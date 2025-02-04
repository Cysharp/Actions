using Zx;

namespace CysharpActions.Tests;

public class GitCommandTest
{
    [Fact]
    public async Task DeleteBranchNotFoundTest()
    {
        if (Environment.GetEnvironmentVariable("CI") is null)
            return;
        if (Environment.GetEnvironmentVariable("GH_REPO") is null)
            throw new Exception("GH_REPO is not set");
        if (Environment.GetEnvironmentVariable("GH_TOKEN") is null)
            throw new Exception("GH_TOKEN is not set");

        var branchName = "it/should/not/exists/at/all";
        var currentBranch = await "git branch --show-current";
        await $"git push origin {currentBranch}:{branchName}";

        var command = new GitCommand();
        var result = await command.DeleteBranchAsync(branchName);
        Assert.False(result);
    }
}
