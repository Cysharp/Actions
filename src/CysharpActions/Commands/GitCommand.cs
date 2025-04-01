using CysharpActions.Contexts;
using CysharpActions.Utils;
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
}