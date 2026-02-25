using CysharpActions.Contexts;
using Zx;

namespace CysharpActions.Tests;

public class GitCommandTest
{
    // Run only on GitHub Actions
    [Fact]
    public async Task DeleteBranchFalse_NotGitHubActionsLoginTest()
    {
        if (!GitHubEnv.Current.CI)
            return;
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
            var result = await command.DeleteBranchAsync(branch);

            // delete before test
            await $"gh api -X DELETE /repos/{GitHubContext.Current.Repository}/git/refs/heads/{branch}";

            Assert.False(result); // because creater is not github-actions[bot]
        }
    }
}
