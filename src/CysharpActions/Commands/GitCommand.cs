using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CysharpActions.Commands;

public class GitCommand()
{
    public async Task<bool> DeleteBranchAsync(string branch)
    {
        // Search branches to delete
        using (var _ = new GitHubActionsGroup($"Searching branch for repo. branch: {branch}"))
        {
            // Check if the branch is the default branch
            var repoJson = await $"gh api /repos/{GitHubContext.Current.Repository}";
            var repo = JsonSerializer.Deserialize<GitHubApiRepo>(repoJson) ?? throw new ActionCommandException("gh api could not get repository info.");

            if (repo.DefaultBranch == branch)
                throw new ActionCommandException($"Branch is default, you cannot delete this branch. branch: {branch}");

            // Check if the branch is created by github-actions[bot]
            var branchesJson = await $"gh api /repos/{GitHubContext.Current.Repository}/branches";
            var branches = JsonSerializer.Deserialize<GitHubApiBranches[]>(branchesJson) ?? throw new ActionCommandException("gh api could not get branches info.");
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
        using (var _ = new GitHubActionsGroup($"Branch detail. branch: {branch}"))
        {
            var branchJson = await $"gh api /repos/{GitHubContext.Current.Repository}/branches/{branch}";
            var branchDetail = JsonSerializer.Deserialize<GitHubApiBranch>(branchJson) ?? throw new ActionCommandException("gh api could not get branch info.");

            GitHubActions.WriteLog($"Checking who created the branch.");

            // Only delete branches created by github-actions[bot]
            if (branchDetail.Commit.Author.Login != "github-actions[bot]")
            {
                GitHubActions.WriteLog($"Branch is not created by github-actions[bot], you cannot delete this branch. branch: {branch}");
                return false;
            }
        }

        using (var _ = new GitHubActionsGroup($"Deleteting branch. branch: {branch}"))
        {
            GitHubActions.WriteLog($"Branch is created by github-actions[bot], deleting branch. branch: {branch}");
            await $"gh api -X DELETE /repos/{GitHubContext.Current.Repository}/git/refs/heads/{branch}";

            GitHubActions.WriteLog($"Branch deleted.");
        }
        return true;
    }

    private class GitHubApiRepo
    {
        [JsonPropertyName("default_branch")]
        public required string DefaultBranch { get; init; }
    }

    private class GitHubApiBranches
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }

    private class GitHubApiBranch
    {
        [JsonPropertyName("commit")]
        public required GitHubApiBranchCommit Commit { get; init; }

        public class GitHubApiBranchCommit
        {
            [JsonPropertyName("author")]
            public required GitHubApiBranchAuthor Author { get; init; }
        }

        public class GitHubApiBranchAuthor
        {
            [JsonPropertyName("login")]
            public required string Login { get; init; }
        }
    }
}