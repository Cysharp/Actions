using CysharpActions.Contexts;
using Zx;

namespace CysharpActions.Tests;

[Collection(LiveGitHubTest.Category)]
[Trait("Category", LiveGitHubTest.Category)]
public class GitCommandLiveTest
{
    [Fact(
        Skip = LiveGitHubTest.SkipReason,
        SkipUnless = nameof(LiveGitHubTest.IsAvailable),
        SkipType = typeof(LiveGitHubTest))]
    public async Task DeleteBranchFalse_NotGitHubActionsLoginTest()
    {
        GHEnv.Current.Validate();

        Zx.Env.useShell = false;
        var branch = "it/should/not/exists/at/all";

        try
        {
            var sha = await "git rev-parse HEAD";
            await $"gh api --method POST -H \"Accept: application/vnd.github.v3+json\" /repos/{GitHubContext.Current.Repository}/git/refs -f ref=\"refs/heads/{branch}\" -f sha=\"{sha}\"";
        }
        finally
        {
            var command = new GitCommand();
            var result = await command.DeleteBranchAsync(branch, TestContext.Current.CancellationToken);

            await $"gh api -X DELETE /repos/{GitHubContext.Current.Repository}/git/refs/heads/{branch}";

            Assert.False(result); // because creator is not github-actions[bot]
        }
    }
}
