using CysharpActions.Contexts;

namespace CysharpActions.Tests;

public class ValidateTagCommandTest
{
    [Theory]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("v0.1.0", "0.1.0")]
    [InlineData("v10.1.0", "10.1.0")]
    public void NormalizeTest(string tag, string expected)
    {
        var command = new ValidateTagCommand(new GitHubReleaseExeDummy());
        var actual = command.Normalize(tag);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1.3.0")]// Current Release Tag is 1.2.0
    [InlineData("999.0.0")]
    [InlineData("1.2.10")] // this should success
    public async Task ValidateSuccessTest(string tag)
    {
        var command = new ValidateTagCommand(new GitHubReleaseExeDummy());
        await command.ValidateTagAsync(tag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0.1.0")]
    [InlineData("1.0.0")]
    public async Task ValidateFailTest(string tag)
    {
        var command = new ValidateTagCommand(new GitHubReleaseExeDummy());
        await Assert.ThrowsAsync<ActionCommandException>(() => command.ValidateTagAsync(tag));
    }
}

public class GitHubReleaseExeDummy() : IGitHubReleaseExe
{
    public async Task<GitHubRelease[]> GetGitHubReleaseAsync()
    {
        // IsLatestは1つだけにしないとだめ
        var versions = new []
        {
            new GitHubRelease { TagName = "1.0.0", IsLatest = false },
            new GitHubRelease { TagName = "1.0.10", IsLatest = false },
            new GitHubRelease { TagName = "1.1.0", IsLatest = false },
            new GitHubRelease { TagName = "1.2.0", IsLatest = false },
            new GitHubRelease { TagName = "1.2.9", IsLatest = true },
        };
        return versions;
    }
}
