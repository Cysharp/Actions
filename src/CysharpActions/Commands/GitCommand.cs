using Cysharp.Diagnostics;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;

using CysharpActions.Runtime;

namespace CysharpActions.Commands;

public class GitCommand
{
    private readonly Func<GitHubCredentials, CancellationToken, Task> configureGit;
    private readonly RunProcess runProcess;
    private readonly RunGitHubCommit runGitHubCommit;

    public GitCommand(
        Func<GitHubCredentials, CancellationToken, Task>? configureGit = null,
        RunProcess? runProcess = null,
        RunGitHubCommit? runGitHubCommit = null)
    {
        this.runProcess = runProcess ?? ProcessRunner.RunAsync;
        this.runGitHubCommit = runGitHubCommit ?? GitHubCommitRunner.RunAsync;
        this.configureGit = configureGit ?? ((credentials, cancellationToken) =>
            GitHelper.SetGitUserEmailAsync(credentials, runProcess: this.runProcess, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteBranchAsync(
        string branch,
        RepositoryContext repositoryContext,
        CancellationToken cancellationToken = default)
    {
        var repository = repositoryContext.RequireRepository();
        // Search branches to delete
        using (var _ = GitHubActions.StartGroup($"Searching branch for repo. branch: {branch}"))
        {
            // Check if the branch is the default branch
            var repoResult = await runProcess(new CommandSpec("gh", ["api", $"/repos/{repository}"]), cancellationToken);
            var repo = JsonSerializer.Deserialize(repoResult.Stdout, JsonSourceGenerationContext.Default.GitHubApiRepo) ?? throw new ActionCommandException("gh api could not get repository info.");

            if (repo.DefaultBranch == branch)
                throw new ActionCommandException($"Branch is default, you cannot delete this branch. branch: {branch}");

            // Check if the branch is created by github-actions[bot]
            var branchesResult = await runProcess(new CommandSpec("gh", ["api", $"/repos/{repository}/branches"]), cancellationToken);
            var branches = JsonSerializer.Deserialize(branchesResult.Stdout, JsonSourceGenerationContext.Default.GitHubApiBranchesArray) ?? throw new ActionCommandException("gh api could not get branches info.");
            if (!branches.Any(x => x.Name == branch))
            {
                GitHubActions.WriteLog($"Branch not exists, exiting. branch: {branch}");
                return false;
            }
            else
            {
                GitHubActions.WriteLog($"branch exists, checking branch detail. branch: {branch}");
            }
        }

        // check branch detail
        using (var _ = GitHubActions.StartGroup($"Branch detail. branch: {branch}"))
        {
            var branchResult = await runProcess(new CommandSpec("gh", ["api", $"/repos/{repository}/branches/{branch}"]), cancellationToken);
            var branchDetail = JsonSerializer.Deserialize(branchResult.Stdout, JsonSourceGenerationContext.Default.GitHubApiBranch) ?? throw new ActionCommandException("gh api could not get branch info.");

            GitHubActions.WriteLog($"Checking who created the branch.");

            // Only delete branches created by github-actions[bot]
            if (branchDetail.Commit.Author.Login != "github-actions[bot]")
            {
                GitHubActions.WriteLog($"Branch is not created by github-actions[bot], you cannot delete this branch. branch: {branch}");
                return false;
            }
        }

        using (var _ = GitHubActions.StartGroup($"Deleteting branch. branch: {branch}"))
        {
            GitHubActions.WriteLog($"Branch is created by github-actions[bot], deleting branch. branch: {branch}");
            await runProcess(new CommandSpec("gh", ["api", "-X", "DELETE", $"/repos/{repository}/git/refs/heads/{branch}"]), cancellationToken);

            GitHubActions.WriteLog($"Branch deleted.");
        }
        return true;
    }

    /// <summary>
    /// Git Commit
    /// </summary>
    /// <param name="dryRun"></param>
    /// <param name="tag"></param>
    /// <param name="modifiedPaths"></param>
    /// <returns></returns>
    public async Task<(bool commited, string sha, string branchName, string isBranchCreated)> CommitAsync(
        bool dryRun,
        string tag,
        string[] modifiedPaths,
        WorkflowRunContext workflowRun,
        GitHubCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var commitPaths = NormalizeCommitPaths(modifiedPaths);
        if (commitPaths.Length == 0)
        {
            GitHubActions.WriteLog("No commit paths specified, skipping commit.");
            return (false, (await RunGitAsync(["rev-parse", "HEAD"], cancellationToken)).Stdout, "", "false");
        }

        GitHubActions.WriteLog($"Set git user.email/user.name if missing ...");
        await configureGit(credentials, cancellationToken);

        // Only stage explicitly allowed paths. -f is required for generated files that may be ignored.
        await RunGitAsync(["add", "-f", "--", .. commitPaths], cancellationToken);

        GitHubActions.WriteLog($"Checking File change has been happen ...");
        var commited = false;
        var branchName = "";
        var isBranchCreated = "false";
        var changedLines = await GetStagedChangesAsync(commitPaths, cancellationToken);
        if (changedLines.Length == 0)
        {
            GitHubActions.WriteLog("Diff not found, skipping commit.");
        }
        else
        {
            GitHubActions.WriteLog("Diff found.");
            if (dryRun)
            {
                GitHubActions.WriteLog("Dryrun Mode detected, creating branch and switch.");
                branchName = $"test-release/{tag}";
                isBranchCreated = "true";
                await RunGitAsync(["switch", "-c", branchName], cancellationToken);
            }

            GitHubActions.WriteLog("Committing change. Running following.");
            await RunGitAsync([
                "commit",
                "--only",
                "-m", $"chore(automate): Update package.json to {tag}",
                "-m", $"Commit by [GitHub Actions]({workflowRun.WorkflowRunUrl})",
                "--",
                .. commitPaths], cancellationToken);

            commited = true;
        }

        var sha = (await RunGitAsync(["rev-parse", "HEAD"], cancellationToken)).Stdout;
        return (commited, sha, branchName, isBranchCreated);
    }

    /// <summary>
    /// Git Commit with sign
    /// </summary>
    /// <param name="dryRun"></param>
    /// <param name="tag"></param>
    /// <param name="modifiedPaths"></param>
    /// <returns></returns>
    public async Task<(bool commited, string sha, string branchName, string isBranchCreated)> CommitWithSignAsync(
        bool dryRun,
        string tag,
        string[] modifiedPaths,
        WorkflowRunContext workflowRun,
        GitHubCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var commitPaths = NormalizeCommitPaths(modifiedPaths);
        if (commitPaths.Length == 0)
        {
            GitHubActions.WriteLog("No commit paths specified, skipping commit.");
            return (false, (await RunGitAsync(["rev-parse", "HEAD"], cancellationToken)).Stdout, "", "false");
        }

        GitHubActions.WriteLog($"Set git user.email/user.name if missing ...");
        await configureGit(credentials, cancellationToken);

        // Only stage explicitly allowed paths. -f is required for generated files that may be ignored.
        await RunGitAsync(["add", "-f", "--", .. commitPaths], cancellationToken);

        GitHubActions.WriteLog($"Checking File change has been happen ...");
        var commited = false;
        var branchName = "";
        var isBranchCreated = "false";
        var changedLines = await GetStagedChangesAsync(commitPaths, cancellationToken);
        if (changedLines.Length == 0)
        {
            GitHubActions.WriteLog("Diff not found, skipping commit.");
        }
        else
        {
            GitHubActions.WriteLog("Diff found.");
            if (dryRun)
            {
                GitHubActions.WriteLog("Dryrun Mode detected, creating branch and switch.");
                branchName = $"test-release/{tag}";
                isBranchCreated = "true";
                await RunGitAsync(["switch", "-c", branchName], cancellationToken);
            }

            var currentBranch = dryRun ? branchName : (await RunGitAsync(["branch", "--show-current"], cancellationToken)).Stdout.Trim();

            GitHubActions.WriteLog("Committing change via GitHub API (signed commit).");

            var token = credentials.RequireToken();
            var (owner, repoName) = credentials.ParseRepository();

            // For non-dryRun, get the remote branch HEAD via API to guarantee the new commit is a fast-forward.
            // For dryRun (new branch), the remote ref does not yet exist, so use local HEAD.
            var headSha = dryRun
                ? (await RunGitAsync(["rev-parse", "HEAD"], cancellationToken)).Stdout.Trim()
                : null;

            GitHubActions.WriteLog($"Building tree with {changedLines.Length} changed files.");
            var treeItems = new List<GitHubTreeItemSpec>(changedLines.Length);
            foreach (var line in changedLines)
            {
                var parts = line.Split('\t', 2);
                if (parts.Length != 2) continue;
                var status = parts[0].Trim();
                var filePath = parts[1].Trim();

                if (status == "D")
                {
                    treeItems.Add(new GitHubTreeItemSpec(filePath, "100644", null, Delete: true));
                }
                else
                {
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    treeItems.Add(new GitHubTreeItemSpec(filePath, GetTreeMode(filePath), content));
                }
            }

            var commitMessage = $"chore(automate): Update package.json to {tag}\n\nCommit by [GitHub Actions]({workflowRun.WorkflowRunUrl})";
            var commitResult = await runGitHubCommit(new GitHubCommitSpec(
                token,
                owner,
                repoName,
                currentBranch,
                headSha,
                commitMessage,
                treeItems,
                AllowForceUpdate: dryRun), cancellationToken);
            switch (commitResult.ReferenceUpdate)
            {
                case GitHubReferenceUpdate.Updated:
                    GitHubActions.WriteLog($"Updated branch reference '{currentBranch}' to {commitResult.Sha}.");
                    break;
                case GitHubReferenceUpdate.Created:
                    GitHubActions.WriteLog($"Created new branch reference '{currentBranch}' at {commitResult.Sha}.");
                    break;
                case GitHubReferenceUpdate.ForceUpdated:
                    GitHubActions.WriteLog($"Force updated branch reference '{currentBranch}' to {commitResult.Sha}.");
                    break;
            }

            // Sync HEAD/index with the remote commit without discarding unrelated working-tree changes.
            await RunGitAsync(["fetch", "origin", currentBranch], cancellationToken);
            await RunGitAsync(["reset", "--mixed", $"origin/{currentBranch}"], cancellationToken);

            GitHubActions.WriteLog("Signed commit created successfully.");
            commited = true;
        }

        var sha = (await RunGitAsync(["rev-parse", "HEAD"], cancellationToken)).Stdout;
        return (commited, sha, branchName, isBranchCreated);
    }

    private static string[] NormalizeCommitPaths(IEnumerable<string> paths)
    {
        return paths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<string[]> GetStagedChangesAsync(string[] commitPaths, CancellationToken cancellationToken)
    {
        return (await RunGitAsync(["diff", "--cached", "HEAD", "--name-status", "--", .. commitPaths], cancellationToken)).OutputLines;
    }

    private Task<ProcessResult> RunGitAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return runProcess(new CommandSpec("git", arguments), cancellationToken);
    }

    private static string GetTreeMode(string filePath)
    {
        // On Windows, file execute bits are not meaningful; default to regular file.
        if (OperatingSystem.IsWindows())
            return "100644";

        var unixMode = File.GetUnixFileMode(filePath);
        var isExecutable = (unixMode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        return isExecutable ? "100755" : "100644";
    }
}
