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
