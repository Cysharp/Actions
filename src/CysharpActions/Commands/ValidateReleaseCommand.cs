using CysharpActions.Runtime;
using CysharpActions.Utils;
using NuGet.Versioning;
using System.IO.Compression;
using System.Xml.Linq;

namespace CysharpActions.Commands;

/// <summary>Validates release inputs without changing the legacy validate-tag contract.</summary>
public sealed class ValidateReleaseCommand(IGitHubReleaseExe releases, RunProcess? runProcess = null)
{
    private readonly RunProcess run = runProcess ?? ProcessRunner.RunAsync;

    /// <summary>Checks Git syntax and NuGet version ordering, returning the version without a v prefix.</summary>
    public async Task<string> ValidateAsync(string tag, RepositoryContext repository, CancellationToken cancellationToken = default)
    {
        var version = ParseTag(tag);
        await run(new CommandSpec("git", ["check-ref-format", $"refs/tags/{tag}"]), cancellationToken);
        // MagicOnion supports multiple major release lines; only skip ordering.
        if (repository.Repository != "Cysharp/MagicOnion")
        {
            var latest = (await releases.GetGitHubReleaseAsync(cancellationToken)).SingleOrDefault(x => x.IsLatest);
            if (latest is not null && VersionComparer.VersionRelease.Compare(version, ParseTag(latest.TagName)) < 0)
                throw new ActionCommandException("Tag is older than the latest release. Please bump the version.");
        }
        return tag.StartsWith('v') ? tag[1..] : tag;
    }

    /// <summary>
    /// Checks that a release run starts from the default branch or from a branch that <paramref name="allowedBranches"/> matches.
    /// workflow_dispatch can start from any branch or tag, and the release tag is created at that commit.
    /// </summary>
    /// <param name="gitRef">Ref of the run (github.ref), for example refs/heads/main.</param>
    /// <param name="refType">Ref type of the run (github.ref_type). Only branch is allowed.</param>
    /// <param name="defaultBranch">Default branch of the repository (github.event.repository.default_branch). Empty fails.</param>
    /// <param name="allowedBranches">Newline-delimited GitHub branch filter patterns allowed in addition to the default branch.</param>
    /// <returns>The branch name without refs/heads/.</returns>
    public static string ValidateRef(string gitRef, string refType, string defaultBranch, string allowedBranches)
    {
        const string BranchRefPrefix = "refs/heads/";
        var filter = BranchFilter.Parse(allowedBranches.ToMultiLine().Select(x => x.Trim()).Where(x => x.Length != 0));
        var allowed = string.Join(", ", filter.Patterns
            .Select(x => $"'{x}'")
            .Prepend(string.IsNullOrWhiteSpace(defaultBranch) ? "(unknown default branch)" : $"'{defaultBranch}' (default branch)"));

        if (string.IsNullOrWhiteSpace(defaultBranch))
            throw Reject("The default branch is unknown because github.event.repository.default_branch is empty. Start the release with workflow_dispatch.");
        if (refType != "branch" || !gitRef.StartsWith(BranchRefPrefix, StringComparison.Ordinal) || gitRef.Length == BranchRefPrefix.Length)
            throw Reject("A release must run from a branch. Tags and other refs are not allowed.");
        var branch = gitRef[BranchRefPrefix.Length..];
        if (branch != defaultBranch && !filter.IsMatch(branch))
            throw Reject($"Branch '{branch}' is not allowed to release.");
        return branch;

        ActionCommandException Reject(string reason)
            => new($"{reason} This run is on '{gitRef}' (ref type '{refType}'). Allowed branches: {allowed}.");
    }

    /// <summary>Parses a package version, allowing a single leading v on Git tags.</summary>
    public static NuGetVersion ParseTag(string tag)
    {
        var value = tag.StartsWith('v') ? tag[1..] : tag;
        if (tag.Any(char.IsWhiteSpace) || !NuGetVersion.TryParse(value, out var version))
            throw new ActionCommandException($"Invalid release version: '{tag}'.");
        return version;
    }

    /// <summary>Validates every package before authentication or publication; metadata is ignored as on NuGet.</summary>
    public static void ValidatePackages(string directory, string version)
    {
        var expected = ParseTag(version);
        var packages = Directory.GetFiles(directory, "*.nupkg");
        if (packages.Length == 0)
            throw new ActionCommandException("No NuGet packages were found.");
        foreach (var path in packages.Concat(Directory.GetFiles(directory, "*.snupkg")))
        {
            using var archive = ZipFile.OpenRead(path);
            var specs = archive.Entries.Where(x => !x.FullName.Contains('/') && x.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (specs.Length != 1)
                throw new ActionCommandException($"Expected one root nuspec in '{path}'.");
            using var stream = specs[0].Open();
            var root = XDocument.Load(stream).Root;
            var ns = root?.Name.Namespace ?? XNamespace.None;
            var actual = root?.Element(ns + "metadata")?.Element(ns + "version")?.Value;
            if (!NuGetVersion.TryParse(actual, out var parsed) || !VersionComparer.VersionRelease.Equals(expected, parsed))
                throw new ActionCommandException($"Package version mismatch in '{path}': expected '{version}', got '{actual}'.");
        }
    }
}
