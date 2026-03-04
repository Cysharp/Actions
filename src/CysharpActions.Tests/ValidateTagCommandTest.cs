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
    [InlineData("1.5.0")]// Current Release Tag is 1.4.0
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

    // 1.0.9 と 1.0.10 の数値比較が正しくできるか (辞書順では "10" < "9" になってしまう)
    [Theory]
    [InlineData("1.0.10")]   // 1.0.10 > 1.0.9
    [InlineData("v1.0.10")]  // v1.0.10 を正規化すると 1.0.10 > 1.0.9
    public async Task ValidateVersionCompare_1_0_9_and_1_0_10_SuccessTest(string tag)
    {
        var command = new ValidateTagCommand(new GitHubReleaseExe109Dummy());
        var normalized = command.Normalize(tag);
        await command.ValidateTagAsync(normalized);
    }

    // v1.0.9-beta1 と v1.0.10-beta1、v1.0.9-beta2 の比較が正しくできるか
    [Theory]
    [InlineData("v1.0.10-beta1")] // 1.0.10-beta1 > 1.0.9-beta1 (patch 数値比較)
    [InlineData("v1.0.9-beta2")]  // 1.0.9-beta2 > 1.0.9-beta1 (pre-release 番号比較)
    [InlineData("v1.1.0")]  // 1.1.0 > 1.0.9-beta1 (patch or prerelease 比較)
    public async Task ValidateVersionCompare_Beta_SuccessTest(string tag)
    {
        var command = new ValidateTagCommand(new GitHubReleaseExeBeta1Dummy());
        var normalized = command.Normalize(tag);
        await command.ValidateTagAsync(normalized);
    }
}

public class GitHubReleaseExeDummy() : IGitHubReleaseExe
{
    public async Task<GitHubRelease[]> GetGitHubReleaseAsync()
    {
        // IsLatestは1つだけにしないとだめ
        var versions = new[]
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

// 1.0.9 が最新リリースであるダミー (1.0.9 vs 1.0.10 の数値比較テスト用)
public class GitHubReleaseExe109Dummy() : IGitHubReleaseExe
{
    public Task<GitHubRelease[]> GetGitHubReleaseAsync()
    {
        var versions = new[]
        {
            new GitHubRelease { TagName = "1.0.9", IsLatest = true },
        };
        return Task.FromResult(versions);
    }
}

// 1.0.9-beta1 が最新リリースであるダミー (beta バージョン比較テスト用)
public class GitHubReleaseExeBeta1Dummy() : IGitHubReleaseExe
{
    public Task<GitHubRelease[]> GetGitHubReleaseAsync()
    {
        var versions = new[]
        {
            new GitHubRelease { TagName = "1.0.9-beta1", IsLatest = true },
        };
        return Task.FromResult(versions);
    }
}
