using CysharpActions.Runtime;
using System.Text.Json;

namespace CysharpActions.Tests;

public class ReleaseLifecycleTest
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Other = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Tag = "v1.2.3";
    private static readonly GitHubCredentials Credentials = new("Cysharp/Actions", "test-token");

    [Fact]
    public async Task NewResourcesAreJournaledAndCleanupIsIdempotent()
    {
        using var remote = new Remote();
        await remote.Create();
        Assert.True(remote.State.CreatedTag);
        Assert.Equal(42, remote.State.ReleaseId);
        Assert.Contains(remote.Calls, x => x.Arguments.Contains($"--force-with-lease=refs/tags/{Tag}:"));
        await remote.Cleanup();
        Assert.Null(remote.TagSha);
        Assert.Null(remote.State.ReleaseId);
        Assert.False(remote.State.CreatedTag);
        var deletes = remote.Deletes;
        await remote.Cleanup();
        Assert.Equal(deletes, remote.Deletes);
    }

    [Theory]
    [InlineData(Head)]
    [InlineData(Other)] // Annotated tag object, peeled to HEAD by rev-parse.
    public async Task MatchingExistingTagIsPreserved(string objectSha)
    {
        using var remote = new Remote { TagSha = objectSha };
        await remote.Create();
        Assert.False(remote.State.CreatedTag);
        await remote.Cleanup();
        Assert.Equal(objectSha, remote.TagSha);
        Assert.Equal(1, remote.Deletes);
    }

    [Fact]
    public async Task MismatchedCommitStopsBeforeReleaseCreation()
    {
        using var remote = new Remote { TagSha = Other, PeeledSha = Other };
        await Assert.ThrowsAsync<ActionCommandException>(remote.Create);
        await remote.Cleanup();
        Assert.Equal(0, remote.Deletes);
        Assert.DoesNotContain(remote.Calls, x => x.Arguments.Contains("POST"));
    }

    [Theory]
    [InlineData("remote")]
    [InlineData("fetch")]
    [InlineData("list")]
    public async Task ReadFailuresNeverBecomeMissingResources(string fail)
    {
        using var remote = new Remote { TagSha = Head, Fail = fail };
        await Assert.ThrowsAsync<InvalidOperationException>(remote.Create);
        Assert.DoesNotContain(remote.Calls, x => x.Arguments.Contains("push") || x.Arguments.Contains("POST"));
    }

    [Fact]
    public async Task ExistingReleaseIsNotAdoptedOrDeleted()
    {
        using var remote = new Remote { TagSha = Head, ExistingRelease = true };
        await Assert.ThrowsAsync<ActionCommandException>(remote.Create);
        await remote.Cleanup();
        Assert.Equal(0, remote.Deletes);
    }

    [Fact]
    public async Task PushFailureDoesNotClaimTagOwnership()
    {
        using var remote = new Remote { Fail = "push" };
        await Assert.ThrowsAsync<InvalidOperationException>(remote.Create);
        Assert.False(remote.State.CreatedTag);
        await remote.Cleanup();
        Assert.Equal(0, remote.Deletes);
    }

    [Fact]
    public async Task ReleaseFailureLeavesTagJournalForCleanup()
    {
        using var remote = new Remote { Fail = "create" };
        await Assert.ThrowsAsync<InvalidOperationException>(remote.Create);
        Assert.True(remote.State.CreatedTag);
        Assert.Null(remote.State.ReleaseId);
        await remote.Cleanup();
        Assert.Null(remote.TagSha);
    }

    [Fact]
    public async Task AssetFailureDoesNotLoseReleaseOwnership()
    {
        using var remote = new Remote();
        await remote.Create();
        var upload = new CreateReleaseCommand(Tag, "title", (_, _) => throw new InvalidOperationException("Upload failed."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => upload.UploadAssetFilesAsync([remote.Path], TestContext.Current.CancellationToken));
        await remote.Cleanup();
        Assert.Equal(2, remote.Deletes);
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("published")]
    [InlineData("wrong-tag")]
    [InlineData("lookup-failed")]
    public async Task CleanupRefusesChangedOrUncertainResources(string change)
    {
        using var remote = new Remote();
        await remote.Create();
        if (change == "moved") remote.TagSha = Other;
        if (change == "published") remote.Draft = false;
        if (change == "wrong-tag") remote.ReleaseTag = "v2.0.0";
        if (change == "lookup-failed") remote.Fail = "get";
        await Assert.ThrowsAnyAsync<Exception>(remote.Cleanup);
        Assert.Equal(0, remote.Deletes);
    }

    [Fact]
    public async Task StateCannotBeReusedOrAppliedToAnotherRepository()
    {
        using var remote = new Remote();
        await remote.Create();
        await Assert.ThrowsAsync<IOException>(remote.Create);
        await Assert.ThrowsAsync<ActionCommandException>(() => new ReleaseLifecycleCommand(remote.Run)
            .CleanupAsync(remote.Path, new("Cysharp/MagicOnion", "test-token"), TestContext.Current.CancellationToken));
        Assert.Equal(0, remote.Deletes);
    }

    [Fact]
    public async Task MissingOrCorruptStateNeverDeletesAnything()
    {
        using var remote = new Remote();
        await Assert.ThrowsAsync<FileNotFoundException>(remote.Cleanup);
        File.WriteAllText(remote.Path, "not-json");
        await Assert.ThrowsAsync<JsonException>(remote.Cleanup);
        Assert.Empty(remote.Calls);
    }

    [Fact]
    public async Task FailedTagDeletionCanResumeWithoutDeletingTheReleaseAgain()
    {
        using var remote = new Remote();
        await remote.Create();
        remote.Fail = "push";
        await Assert.ThrowsAsync<InvalidOperationException>(remote.Cleanup);
        Assert.Null(remote.State.ReleaseId);
        Assert.True(remote.State.CreatedTag);
        Assert.Equal(1, remote.Deletes);
        remote.Fail = null;
        await remote.Cleanup();
        Assert.Equal(2, remote.Deletes);
        Assert.Contains(remote.Calls, x => x.Arguments.Contains($"--force-with-lease=refs/tags/{Tag}:{Head}"));
    }

    private sealed class Remote : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
        public string? TagSha { get; set; }
        public string PeeledSha { get; set; } = Head;
        public string? Fail { get; set; }
        public bool ExistingRelease { get; set; }
        public bool Draft { get; set; } = true;
        public string ReleaseTag { get; set; } = Tag;
        public int Deletes { get; private set; }
        public List<CommandSpec> Calls { get; } = [];
        public ReleaseState State => JsonSerializer.Deserialize<ReleaseState>(File.ReadAllText(Path))!;
        public Task Create() => new ReleaseLifecycleCommand(Run).CreateAsync(Tag, "title", Path, Credentials);
        public Task Cleanup() => new ReleaseLifecycleCommand(Run).CleanupAsync(Path, Credentials);

        public Task<ProcessResult> Run(CommandSpec spec, CancellationToken cancellationToken)
        {
            Calls.Add(spec);
            var args = spec.Arguments;
            string output = "";
            if (spec.FileName == "git")
            {
                switch (args[0])
                {
                    case "ls-remote":
                        Check("remote");
                        output = TagSha is null ? "" : $"{TagSha}\trefs/tags/{Tag}";
                        break;
                    case "fetch": Check("fetch"); break;
                    case "rev-parse":
                        output = args[^1] switch { "FETCH_HEAD" => TagSha!, "FETCH_HEAD^{commit}" => PeeledSha, _ => Head };
                        break;
                    case "push":
                        Check("push");
                        if (args[^1].StartsWith(':')) { TagSha = null; Deletes++; }
                        else { TagSha = Head; output = $"*\t{Head}:refs/tags/{Tag}\t[new tag]"; }
                        break;
                }
            }
            else if (args.Contains("--slurp"))
            {
                Check("list");
                output = ExistingRelease ? $"[[{{\"tag_name\":\"{Tag}\"}}]]" : "[[]]";
            }
            else if (args.Contains("POST")) { Check("create"); output = "{\"id\":42}"; }
            else if (args.Contains("DELETE")) { Deletes++; }
            else
            {
                Check("get");
                output = JsonSerializer.Serialize(new { id = 42, tag_name = ReleaseTag, draft = Draft });
            }
            return Task.FromResult(new ProcessResult(output, ""));
        }

        private void Check(string stage)
        {
            if (Fail == stage) throw new InvalidOperationException($"Failure at {stage}.");
        }

        public void Dispose() => File.Delete(Path);
    }
}
