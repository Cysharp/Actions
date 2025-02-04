using CysharpActions.Contexts;
using Zx;

namespace CysharpActions.Tests;

public class GitCommandTest
{
    // Run only on GitHub Actions
    [Fact]
    public async Task DeleteBrancFalse_NotGitHubActionsLoginTest()
    {
        if (Environment.GetEnvironmentVariable("CI") is null)
            return;
        if (Environment.GetEnvironmentVariable("GH_REPO") is null)
            throw new Exception("GH_REPO is not set");
        if (Environment.GetEnvironmentVariable("GH_TOKEN") is null)
            throw new Exception("GH_TOKEN is not set");

        Zx.Env.useShell = false;

        var branch = "it/should/not/exists/at/all";

        try
        {
            // delete before test
            await $"gh api -X DELETE /repos/{GitHubContext.Current.Repository}/git/refs/heads/{branch}";

            var sha = await "git rev-parse HEAD";
            await $"gh api --method POST -H \"Accept: application/vnd.github.v3+json\" /repos/{GitHubContext.Current.Repository}/git/refs -f ref=\"refs/heads/{branch}\" -f sha=\"{sha}\"";
        }
        finally
        {
            var command = new GitCommand();
            var result = await command.DeleteBranchAsync(branch);
            Assert.False(result); // because creater is not github-actions[bot]
        }
    }
}
