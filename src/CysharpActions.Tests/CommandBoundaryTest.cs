using CysharpActions.Runtime;

namespace CysharpActions.Tests;

public class CommandBoundaryTest
{
    [Fact]
    public async Task UploadAssetPassesPathAsOneArgumentTest()
    {
        var dir = $".tests/{nameof(CommandBoundaryTest)}/{nameof(UploadAssetPassesPathAsOneArgumentTest)}";
        var path = Path.Combine(dir, "asset with spaces.zip");
        CommandSpec? actual = null;

        Task<ProcessResult> Run(CommandSpec command, CancellationToken cancellationToken)
        {
            actual = command;
            return Task.FromResult(new ProcessResult(0, "", ""));
        }

        try
        {
            CreateFile(path, "asset");
            var command = new CreateReleaseCommand("v1.2.3", "Release 1.2.3", Run);
            await command.UploadAssetFilesAsync([path], TestContext.Current.CancellationToken);

            Assert.NotNull(actual);
            Assert.Equal("gh", actual.Value.FileName);
            Assert.Equal(["release", "upload", "v1.2.3", path], actual.Value.Arguments);
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task GitHubReleaseExePassesStructuredArgumentsAndParsesResultTest()
    {
        CommandSpec? actual = null;

        Task<ProcessResult> Run(CommandSpec command, CancellationToken cancellationToken)
        {
            actual = command;
            return Task.FromResult(new ProcessResult(0, "[{\"tagName\":\"1.2.3\",\"isLatest\":true}]", ""));
        }

        var releases = await new GitHubReleaseExeGh(Run).GetGitHubReleaseAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(actual);
        Assert.Equal("gh", actual.Value.FileName);
        Assert.Equal(
            ["release", "list", "--exclude-drafts", "--exclude-pre-releases", "--json", "tagName,isLatest"],
            actual.Value.Arguments);
        var release = Assert.Single(releases);
        Assert.Equal("1.2.3", release.TagName);
        Assert.True(release.IsLatest);
    }
}
