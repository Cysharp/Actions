using CysharpActions.Contexts;
using Zx;

using CysharpActions.Runtime;

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

            var command = new GitCommand((_, _) => Task.CompletedTask);
            var result = await command.CommitAsync(
                false,
                "1.0.0",
                [targetPath, unchangedPath],
                new WorkflowRunContext("https://github.com", "owner/repository", "1"),
                new GitHubCredentials("owner/repository", "token"),
                TestContext.Current.CancellationToken);

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

}

[CollectionDefinition("Git environment", DisableParallelization = true)]
public sealed class GitEnvironmentCollection;
