using CysharpActions.Contexts;
using Zx;

namespace CysharpActions.Tests;

[Collection("Git environment")]
public class GitCommandTest
{
    [Fact]
    public async Task CommitAsyncCommitsOnlyExplicitPathsTest()
    {
        var baseDirectory = Path.GetFullPath($".tests/{nameof(GitCommandTest)}/{nameof(CommitAsyncCommitsOnlyExplicitPathsTest)}");
        const string targetPath = "target file.txt";
        const string unchangedPath = "unchanged.txt";
        const string unrelatedPath = "unrelated.txt";
        const string untrackedPath = "untracked.txt";
        var originalWorkingDirectory = Zx.Env.workingDirectory;
        try
        {
            Directory.CreateDirectory(baseDirectory);
            Zx.Env.workingDirectory = baseDirectory;

            await "git init -b main";
            await "git config user.email test@example.com";
            await "git config user.name Test";
            await "git config commit.gpgsign false";
            CreateFile(Path.Combine(baseDirectory, targetPath), "before");
            CreateFile(Path.Combine(baseDirectory, unchangedPath), "unchanged");
            CreateFile(Path.Combine(baseDirectory, unrelatedPath), "before");
            await "git add -- .";
            await "git commit -m initial";

            File.WriteAllText(Path.Combine(baseDirectory, targetPath), "after");
            File.WriteAllText(Path.Combine(baseDirectory, unrelatedPath), "after");
            File.WriteAllText(Path.Combine(baseDirectory, untrackedPath), "untracked");
            await $"git add -- {unrelatedPath}";

            var command = new GitCommand(() => Task.CompletedTask);
            var result = await command.CommitAsync(false, "1.0.0", [targetPath, unchangedPath]);

            Assert.True(result.commited);
            Assert.Equal(targetPath, (await "git show --pretty=format: --name-only HEAD").Trim());
            Assert.Equal(unrelatedPath, (await "git diff --cached --name-only").Trim());
            Assert.Contains($"?? {untrackedPath}", await "git status --short", StringComparison.Ordinal);
        }
        finally
        {
            Zx.Env.workingDirectory = originalWorkingDirectory;
            SafeDeleteDirectory(baseDirectory);
        }
    }

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

[CollectionDefinition("Git environment", DisableParallelization = true)]
public sealed class GitEnvironmentCollection;
