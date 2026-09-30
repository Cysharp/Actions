using CysharpActions.Runtime;
using CysharpActions.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CysharpActions.Commands;

/// <summary>Creates and cleans up releases using a durable record of resources created by this invocation.</summary>
public sealed class ReleaseLifecycleCommand(RunProcess? runProcess = null)
{
    private readonly RunProcess run = runProcess ?? ProcessRunner.RunAsync;

    /// <summary>Creates a draft at HEAD, recording ownership immediately after each successful mutation.</summary>
    public async Task CreateAsync(string tag, string title, string statePath, GitHubCredentials credentials, CancellationToken cancellationToken = default)
    {
        credentials.Validate();
        ValidateReleaseCommand.ParseTag(tag);
        await Git("check-ref-format", $"refs/tags/{tag}");
        var head = (await Git("rev-parse", "--verify", "HEAD^{commit}")).Stdout.Trim();
        ValidateSha(head);
        var repository = credentials.Repository!;
        var state = new ReleaseState { Repository = repository, Tag = tag, CommitSha = head };
        // Never adopt or overwrite a state file from another invocation.
        using (var file = new FileStream(statePath, FileMode.CreateNew, FileAccess.Write))
            JsonSerializer.Serialize(file, state, ReleaseStateJson.Default.ReleaseState);

        var remote = await RemoteTagAsync(tag, cancellationToken);
        if (remote is not null)
        {
            await Git("fetch", "--no-tags", "origin", $"refs/tags/{tag}");
            var fetched = (await Git("rev-parse", "--verify", "FETCH_HEAD")).Stdout.Trim();
            var commit = (await Git("rev-parse", "--verify", "FETCH_HEAD^{commit}")).Stdout.Trim();
            if (fetched != remote || commit != head)
                throw new ActionCommandException("Existing tag does not match the build commit, or changed during validation.");
        }

        // Listing errors must fail closed, rather than being interpreted as a missing release.
        using (var releases = JsonDocument.Parse((await Gh("api", $"repos/{repository}/releases", "--paginate", "--slurp")).Stdout))
        {
            if (releases.RootElement.EnumerateArray().SelectMany(page => page.EnumerateArray())
                .Any(release => release.GetProperty("tag_name").GetString() == tag))
                throw new ActionCommandException("A release already exists for this tag.");
        }

        if (remote is null)
        {
            await GitHelper.SetGitUserEmailAsync(credentials, runProcess: run, cancellationToken: cancellationToken);
            // Git can report "up to date" despite an empty lease when another actor
            // creates the same tag at the same SHA. Only claim an acknowledged new ref.
            var pushed = await Git("push", "--porcelain", $"--force-with-lease=refs/tags/{tag}:", "origin", $"{head}:refs/tags/{tag}");
            if (!pushed.OutputLines.Any(line => line.StartsWith($"*\t{head}:refs/tags/{tag}\t", StringComparison.Ordinal)))
                throw new ActionCommandException("Tag creation was not acknowledged as a new ref; refusing to claim ownership.");
            state.CreatedTag = true;
            state.TagObjectSha = head;
            Save(statePath, state);
        }
        else
        {
            state.TagObjectSha = remote;
            Save(statePath, state);
        }
        if (await RemoteTagAsync(tag, cancellationToken) != state.TagObjectSha)
            throw new ActionCommandException("Remote tag changed before release creation.");

        using var response = JsonDocument.Parse((await Gh("api", $"repos/{repository}/releases", "--method", "POST",
            "-f", $"tag_name={tag}", "-f", $"target_commitish={head}", "-f", $"name={title}",
            "-F", "draft=true", "-F", "generate_release_notes=true")).Stdout);
        var releaseId = response.RootElement.GetProperty("id").GetInt64();
        if (releaseId <= 0)
            throw new ActionCommandException("Release creation returned an invalid ID; manual recovery is required.");
        state.ReleaseId = releaseId;
        Save(statePath, state);

        Task<ProcessResult> Git(params string[] args) => run(new CommandSpec("git", args), cancellationToken);
        Task<ProcessResult> Gh(params string[] args) => run(new CommandSpec("gh", args), cancellationToken);
    }

    /// <summary>Deletes only recorded resources, preserving preexisting tags and releases.</summary>
    public async Task CleanupAsync(string statePath, GitHubCredentials credentials, CancellationToken cancellationToken = default)
    {
        credentials.Validate();
        // Missing, corrupt or uncertain ownership is never a reason to delete remote resources.
        GitHubActions.WriteLog($"Release ownership journal: {statePath}");
        var state = JsonSerializer.Deserialize(File.ReadAllText(statePath), ReleaseStateJson.Default.ReleaseState)
            ?? throw new ActionCommandException("Missing release ownership state.");
        using (GitHubActions.StartGroup("Recorded release ownership before cleanup"))
        {
            GitHubActions.WriteLog($"SchemaVersion: {state.SchemaVersion}");
            GitHubActions.WriteLog($"Repository: {state.Repository}");
            GitHubActions.WriteLog($"Tag: {state.Tag}");
            GitHubActions.WriteLog($"CommitSha (build commit): {state.CommitSha}");
            GitHubActions.WriteLog($"TagObjectSha (expected remote tag object): {state.TagObjectSha}");
            GitHubActions.WriteLog($"CreatedTag (created by this run and not yet deleted): {state.CreatedTag}");
            GitHubActions.WriteLog($"ReleaseId (created by this run and not yet deleted): {state.ReleaseId?.ToString() ?? "none"}");
        }
        if (state.SchemaVersion != 1 || state.Repository != credentials.Repository)
            throw new ActionCommandException("Release ownership state does not match this repository.");
        ValidateReleaseCommand.ParseTag(state.Tag);
        ValidateSha(state.CommitSha);
        await run(new CommandSpec("git", ["check-ref-format", $"refs/tags/{state.Tag}"]), cancellationToken);
        if (!state.CreatedTag && state.ReleaseId is null)
        {
            GitHubActions.WriteLog("No owned resources remain to clean up.");
            return;
        }
        ValidateSha(state.TagObjectSha);
        if (state.CreatedTag && state.TagObjectSha != state.CommitSha)
            throw new ActionCommandException("Created tag ownership does not match the recorded commit.");
        var remote = await RemoteTagAsync(state.Tag, cancellationToken);
        if (remote != state.TagObjectSha && (remote is not null || state.ReleaseId is not null))
            throw new ActionCommandException("Tag changed since creation; refusing cleanup.");
        if (state.ReleaseId is { } id)
        {
            if (id <= 0)
                throw new ActionCommandException("Invalid release ID in ownership state.");
            var endpoint = $"repos/{state.Repository}/releases/{id}";
            using var release = JsonDocument.Parse((await run(new CommandSpec("gh", ["api", endpoint]), cancellationToken)).Stdout);
            if (release.RootElement.GetProperty("id").GetInt64() != id ||
                release.RootElement.GetProperty("tag_name").GetString() != state.Tag ||
                !release.RootElement.GetProperty("draft").GetBoolean())
                throw new ActionCommandException("Release changed or was published; refusing cleanup.");
            await run(new CommandSpec("gh", ["api", endpoint, "--method", "DELETE"]), cancellationToken);
            state.ReleaseId = null;
            Save(statePath, state);
            GitHubActions.WriteLog($"Deleted owned draft release: {id}");
        }
        if (state.CreatedTag)
        {
            if (remote is not null)
            {
                await GitHelper.SetGitUserEmailAsync(credentials, runProcess: run, cancellationToken: cancellationToken);
                await run(new CommandSpec("git", ["push", $"--force-with-lease=refs/tags/{state.Tag}:{state.TagObjectSha}",
                    "origin", $":refs/tags/{state.Tag}"]), cancellationToken);
            }
            state.CreatedTag = false;
            Save(statePath, state);
            GitHubActions.WriteLog($"Owned tag removed or already absent: {state.Tag}");
        }
        else
        {
            GitHubActions.WriteLog($"Preserved pre-existing tag: {state.Tag}");
        }
        GitHubActions.WriteLog("Cleanup completed. No owned resources remain in the journal.");
    }

    private async Task<string?> RemoteTagAsync(string tag, CancellationToken cancellationToken)
    {
        var reference = $"refs/tags/{tag}";
        var result = await run(new CommandSpec("git", ["ls-remote", "--refs", "origin", reference]), cancellationToken);
        if (string.IsNullOrWhiteSpace(result.Stdout))
            return null;
        var fields = result.Stdout.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 2 || fields[1] != reference)
            throw new ActionCommandException("Unexpected remote tag response.");
        ValidateSha(fields[0]);
        return fields[0];
    }

    private static void ValidateSha(string sha)
    {
        if (!Regex.IsMatch(sha, "\\A(?:[0-9a-f]{40}|[0-9a-f]{64})\\z", RegexOptions.CultureInvariant))
            throw new ActionCommandException("Invalid Git object ID.");
    }

    private static void Save(string path, ReleaseState state)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, ReleaseStateJson.Default.ReleaseState));
        File.Move(temporary, path, overwrite: true);
    }
}

/// <summary>Ownership journal for a single release invocation. Keep this file local to the workflow run.</summary>
public sealed class ReleaseState
{
    /// <summary>Journal format version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Repository owning the resources.</summary>
    public string Repository { get; set; } = "";
    /// <summary>Original Git tag name.</summary>
    public string Tag { get; set; } = "";
    /// <summary>Build commit recorded at creation.</summary>
    public string CommitSha { get; set; } = "";
    /// <summary>Raw remote tag object ID, used as the cleanup lease.</summary>
    public string TagObjectSha { get; set; } = "";
    /// <summary>Whether this invocation created the tag and has not deleted it.</summary>
    public bool CreatedTag { get; set; }
    /// <summary>ID of the draft created by this invocation, until deleted.</summary>
    public long? ReleaseId { get; set; }
}

[JsonSerializable(typeof(ReleaseState))]
internal partial class ReleaseStateJson : JsonSerializerContext;
