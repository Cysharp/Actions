using CysharpActions.Runtime;
using CysharpActions.Contexts;
using System.IO.Compression;

namespace CysharpActions.Tests;

public class ReleaseValidationTest
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3-preview.1", "1.2.3-preview.1")]
    [InlineData("1.2.3+build.4", "1.2.3+build.4")]
    public async Task NormalizesAndValidatesGitRef(string tag, string expected)
    {
        CommandSpec? actual = null;
        var command = new ValidateReleaseCommand(new Releases(), (spec, _) =>
        {
            actual = spec;
            return Task.FromResult(new ProcessResult("", ""));
        });
        Assert.Equal(expected, await command.ValidateAsync(tag, new("Cysharp/Actions"), TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "check-ref-format", $"refs/tags/{tag}" }, actual!.Value.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("not-a-version")]
    [InlineData("1.2.3\ninjected=true")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3-")]
    public async Task InvalidFormatIsRejectedEvenForMagicOnion(string tag)
    {
        var command = new ValidateReleaseCommand(new Releases(), (_, _) => throw new Exception("Must reject before Git."));
        await Assert.ThrowsAsync<ActionCommandException>(() => command.ValidateAsync(tag, new("Cysharp/MagicOnion"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GitFailureIsNotIgnoredForMagicOnion()
    {
        var command = new ValidateReleaseCommand(new Releases(), (_, _) => throw new InvalidOperationException("Invalid ref."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ValidateAsync("1.2.3-lock.lock", new("Cysharp/MagicOnion"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LatestPrefixedTagUsesNumericComparison()
    {
        var command = new ValidateReleaseCommand(new Releases("v1.0.10"), (_, _) => Task.FromResult(new ProcessResult("", "")));
        await Assert.ThrowsAsync<ActionCommandException>(() => command.ValidateAsync("1.0.9", new("Cysharp/Actions"), TestContext.Current.CancellationToken));
        Assert.Equal("1.0.10", await command.ValidateAsync("v1.0.10", new("Cysharp/Actions"), TestContext.Current.CancellationToken));
        Assert.Equal("1.0.9", await command.ValidateAsync("1.0.9", new("Cysharp/MagicOnion"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReleaseQueryFailureIsNotTreatedAsNoReleases()
    {
        var command = new ValidateReleaseCommand(new Releases(fail: true), (_, _) => Task.FromResult(new ProcessResult("", "")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ValidateAsync("1.0.0", new("Cysharp/Actions"), TestContext.Current.CancellationToken));
    }

    [Theory]
    // The default branch is read from the event, not hardcoded: both main and master work.
    [InlineData("refs/heads/main", "main", "", "main")]
    [InlineData("refs/heads/master", "master", "", "master")]
    // The default branch stays allowed even when a negative pattern matches it.
    [InlineData("refs/heads/main", "main", "!main", "main")]
    // MagicOnion releases from release/*.
    [InlineData("refs/heads/release/6.x", "main", "release/*", "release/6.x")]
    [InlineData("refs/heads/release/6.x/foo", "main", "release/**", "release/6.x/foo")]
    // MessagePack releases from v2, v3, v4 and similar; lines are trimmed and blank lines are ignored.
    [InlineData("refs/heads/v3", "master", "v2\n  v3  \r\n\nv4\n", "v3")]
    [InlineData("refs/heads/v10", "master", "v[0-9]+", "v10")]
    public void AllowedReleaseRefReturnsBranch(string gitRef, string defaultBranch, string allowedBranches, string expected)
    {
        Assert.Equal(expected, ValidateReleaseCommand.ValidateRef(gitRef, "branch", defaultBranch, allowedBranches));
    }

    [Theory]
    // Other branches are rejected, including main when the default branch is master.
    [InlineData("refs/heads/feature/x", "branch", "main", "release/*")]
    [InlineData("refs/heads/main", "branch", "master", "")]
    // * does not cross /.
    [InlineData("refs/heads/release/6.x/foo", "branch", "main", "release/*")]
    // Loose patterns are not needed: explicit lines and digits-only patterns reject a fork branch such as v4_test.
    [InlineData("refs/heads/v4_test", "branch", "master", "v2\nv3\nv4")]
    [InlineData("refs/heads/v4_test", "branch", "master", "v[0-9]+")]
    // Tags are never allowed, even when the tag name matches the default branch or a pattern.
    [InlineData("refs/tags/v1.0.0", "tag", "main", "**")]
    [InlineData("refs/tags/main", "tag", "main", "")]
    [InlineData("refs/heads/main", "tag", "main", "")]
    // Pull request and other non-branch refs are rejected.
    [InlineData("refs/pull/1/merge", "branch", "main", "**")]
    [InlineData("refs/heads/", "branch", "main", "**")]
    [InlineData("main", "branch", "main", "")]
    [InlineData("", "", "main", "**")]
    // Fail closed when the event has no default branch, for example a trigger that is not workflow_dispatch.
    [InlineData("refs/heads/main", "branch", "", "")]
    [InlineData("refs/heads/release/6.x", "branch", " ", "release/*")]
    public void RejectedReleaseRefShowsRefAndAllowedBranches(string gitRef, string refType, string defaultBranch, string allowedBranches)
    {
        var exception = Assert.Throws<ActionCommandException>(() => ValidateReleaseCommand.ValidateRef(gitRef, refType, defaultBranch, allowedBranches));
        Assert.Contains($"This run is on '{gitRef}' (ref type '{refType}').", exception.Message, StringComparison.Ordinal);
        Assert.Contains(string.IsNullOrWhiteSpace(defaultBranch) ? "(unknown default branch)" : $"'{defaultBranch}' (default branch)", exception.Message, StringComparison.Ordinal);
        foreach (var pattern in allowedBranches.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Assert.Contains($"'{pattern}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAllowedBranchPatternFailsEvenOnDefaultBranch()
    {
        // A typo must not be ignored only because the run happens to be on the default branch.
        var exception = Assert.Throws<ActionCommandException>(() => ValidateReleaseCommand.ValidateRef("refs/heads/main", "branch", "main", "release/[6-x]"));
        Assert.Contains("'release/[6-x]'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllPackagesMustMatchIncludingSymbols()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Throws<ActionCommandException>(() => ValidateReleaseCommand.ValidatePackages(directory, "1.2.3"));
            Package("first.nupkg", "01.2.3.0+build.1");
            ValidateReleaseCommand.ValidatePackages(directory, "v1.2.3");
            Package("first.snupkg", "1.2.4");
            Assert.Throws<ActionCommandException>(() => ValidateReleaseCommand.ValidatePackages(directory, "1.2.3"));
        }
        finally { Directory.Delete(directory, recursive: true); }

        void Package(string file, string version)
        {
            using var archive = ZipFile.Open(Path.Combine(directory, file), ZipArchiveMode.Create);
            using var writer = new StreamWriter(archive.CreateEntry("first.nuspec").Open());
            writer.Write($"<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><version>{version}</version></metadata></package>");
        }
    }

    private sealed class Releases(string? latest = null, bool fail = false) : IGitHubReleaseExe
    {
        public Task<GitHubRelease[]> GetGitHubReleaseAsync(CancellationToken cancellationToken = default)
            => fail ? throw new InvalidOperationException("API unavailable.")
                : Task.FromResult<GitHubRelease[]>(latest is null ? [] : [new() { TagName = latest, IsLatest = true }]);
    }
}
