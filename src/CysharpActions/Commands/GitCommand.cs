using Cysharp.Diagnostics;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using Octokit;
using System.Text.Json;

namespace CysharpActions.Commands;

public class GitCommand()
{
    public async Task<bool> DeleteBranchAsync(string branch)
    {
        Env.useShell = false;

        // Search branches to delete
        using (var _ = GitHubActions.StartGroup($"Searching branch for repo. branch: {branch}"))
        {
            // Check if the branch is the default branch
            var repoJson = await $"gh api /repos/{GitHubContext.Current.Repository}";
            var repo = JsonSerializer.Deserialize(repoJson, JsonSourceGenerationContext.Default.GitHubApiRepo) ?? throw new ActionCommandException("gh api could not get repository info.");

            if (repo.DefaultBranch == branch)
                throw new ActionCommandException($"Branch is default, you cannot delete this branch. branch: {branch}");

            // Check if the branch is created by github-actions[bot]
            var branchesJson = await $"gh api /repos/{GitHubContext.Current.Repository}/branches";
            var branches = JsonSerializer.Deserialize(branchesJson, JsonSourceGenerationContext.Default.GitHubApiBranchesArray) ?? throw new ActionCommandException("gh api could not get branches info.");
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
            var branchJson = await $"gh api /repos/{GitHubContext.Current.Repository}/branches/{branch}";
            var branchDetail = JsonSerializer.Deserialize(branchJson, JsonSourceGenerationContext.Default.GitHubApiBranch) ?? throw new ActionCommandException("gh api could not get branch info.");

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
            await $"gh api -X DELETE /repos/{GitHubContext.Current.Repository}/git/refs/heads/{branch}";

            GitHubActions.WriteLog($"Branch deleted.");
        }
        return true;
    }

    /// <summary>
    /// Git Commit
    /// </summary>
    /// <param name="dryRun"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task<(bool commited, string sha, string branchName, string isBranchCreated)> CommitAsync(bool dryRun, string tag)
    {
        Env.useShell = false;

        GitHubActions.WriteLog($"Set git user.email/user.name if missing ...");
        await GitHelper.SetGitUserEmailAsync();

        GitHubActions.WriteLog($"Checking File change has been happen ...");
        var commited = false;
        var branchName = "";
        var isBranchCreated = "false";
        try
        {
            var result = await "git diff --exit-code"; // 0 = no diff, 1 = diff
            GitHubActions.WriteLog("Diff not found, skipping commit.");
        }
        catch (ProcessErrorException)
        {
            GitHubActions.WriteLog("Diff found.");
            if (dryRun)
            {
                GitHubActions.WriteLog("Dryrun Mode detected, creating branch and switch.");
                branchName = $"test-release/{tag}";
                isBranchCreated = "true";
                await $"git switch -c {branchName}";
            }

            GitHubActions.WriteLog("Committing change. Running following.");
            await $"git commit -a -m \"{$"chore(automate): Update package.json to {tag}"}\" -m \"{$"Commit by [GitHub Actions]({GitHubContext.Current.WorkflowRunUrl})"}\"";

            commited = true;
        }

        var sha = await "git rev-parse HEAD";
        return (commited, sha, branchName, isBranchCreated);
    }

    /// <summary>
    /// Git Commit with sign
    /// </summary>
    /// <param name="dryRun"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task<(bool commited, string sha, string branchName, string isBranchCreated)> CommitWithSignAsync(bool dryRun, string tag)
    {
        Env.useShell = false;

        GitHubActions.WriteLog($"Set git user.email/user.name if missing ...");
        await GitHelper.SetGitUserEmailAsync();

        GitHubActions.WriteLog($"Checking File change has been happen ...");
        var commited = false;
        var branchName = "";
        var isBranchCreated = "false";
        try
        {
            var result = await "git diff --exit-code"; // 0 = no diff, 1 = diff
            GitHubActions.WriteLog("Diff not found, skipping commit.");
        }
        catch (ProcessErrorException)
        {
            GitHubActions.WriteLog("Diff found.");
            if (dryRun)
            {
                GitHubActions.WriteLog("Dryrun Mode detected, creating branch and switch.");
                branchName = $"test-release/{tag}";
                isBranchCreated = "true";
                await $"git switch -c {branchName}";
            }

            var currentBranch = dryRun ? branchName : await "git branch --show-current";

            GitHubActions.WriteLog("Committing change via GitHub API (signed commit).");

            var token = GHEnv.Current.GH_TOKEN ?? throw new ActionCommandException("GH_TOKEN is required.");
            var repoEnv = GHEnv.Current.GH_REPO ?? throw new ActionCommandException("GH_REPO is required.");
            var separatorIndex = repoEnv.IndexOf('/');
            if (separatorIndex < 0)
                throw new ActionCommandException($"Invalid repository format: {repoEnv}");
            var owner = repoEnv[..separatorIndex];
            var repoName = repoEnv[(separatorIndex + 1)..];

            var client = new GitHubClient(new ProductHeaderValue("CysharpActions"))
            {
                Credentials = new Credentials(token)
            };

            // Get current HEAD commit and its tree SHA
            var headSha = (await "git rev-parse HEAD").Trim();
            var currentCommit = await client.Git.Commit.Get(owner, repoName, headSha);
            var baseTreeSha = currentCommit.Tree.Sha;

            // Collect changed files relative to HEAD (staged and unstaged)
            var diffOutput = await "git diff HEAD --name-status";
            var changedLines = diffOutput.ToMultiLine();

            // Create tree with changed files, this is key for signed commit because we don't use GPG or SSH signing key.
            // Instead, we create a new tree with the changed files and reference it in the commit. GitHub will verify the commit content and sign it if it matches the tree.
            GitHubActions.WriteLog($"Building tree with {changedLines.Length} changed files.");
            var newTree = new NewTree { BaseTree = baseTreeSha };
            foreach (var line in changedLines)
            {
                var parts = line.Split('\t', 2);
                if (parts.Length != 2) continue;
                var status = parts[0].Trim();
                var filePath = parts[1].Trim();

                if (status == "D")
                {
                    newTree.Tree.Add(new NewTreeItem
                    {
                        Path = filePath,
                        Mode = "100644",
                        Type = TreeType.Blob,
                        Sha = null,
                    });
                }
                else
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    newTree.Tree.Add(new NewTreeItem
                    {
                        Path = filePath,
                        Mode = GetTreeMode(filePath),
                        Type = TreeType.Blob,
                        Content = content,
                    });
                }
            }

            var treeResponse = await client.Git.Tree.Create(owner, repoName, newTree);

            // Create signed commit via GitHub API
            var commitMessage = $"chore(automate): Update package.json to {tag}\n\nCommit by [GitHub Actions]({GitHubContext.Current.WorkflowRunUrl})";
            var newCommit = new NewCommit(commitMessage, treeResponse.Sha, [headSha]);
            var createdCommit = await client.Git.Commit.Create(owner, repoName, newCommit);

            // Update or create the remote branch reference
            try
            {
                await client.Git.Reference.Update(owner, repoName, $"heads/{currentBranch}", new ReferenceUpdate(createdCommit.Sha));
                GitHubActions.WriteLog($"Updated branch reference '{currentBranch}' to {createdCommit.Sha}.");
            }
            catch (ApiException ex) when (ex.Message.Contains("Reference does not exist"))
            {
                await client.Git.Reference.Create(owner, repoName, new NewReference($"refs/heads/{currentBranch}", createdCommit.Sha));
                GitHubActions.WriteLog($"Created new branch reference '{currentBranch}' at {createdCommit.Sha}.");
            }

            // Sync local repo with the remote commit
            await $"git fetch origin {currentBranch}";
            await $"git reset --hard origin/{currentBranch}";

            GitHubActions.WriteLog("Signed commit created successfully.");
            commited = true;
        }

        var sha = await "git rev-parse HEAD";
        return (commited, sha, branchName, isBranchCreated);
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