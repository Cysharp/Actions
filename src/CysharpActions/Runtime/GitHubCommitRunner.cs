using Octokit;

namespace CysharpActions.Runtime;

public readonly record struct GitHubTreeItemSpec(
    string Path,
    string Mode,
    string? Content,
    bool Delete = false)
{
    public override string ToString() =>
        $"GitHubTreeItemSpec {{ Path = {Path}, Mode = {Mode}, Content = <omitted>, Delete = {Delete} }}";
}

public readonly record struct GitHubCommitSpec(
    string Token,
    string Owner,
    string Repository,
    string Branch,
    string? HeadSha,
    string Message,
    IReadOnlyList<GitHubTreeItemSpec> TreeItems,
    bool AllowForceUpdate = false)
{
    public override string ToString() =>
        $"GitHubCommitSpec {{ Token = ***, Repository = {Owner}/{Repository}, Branch = {Branch}, TreeItems = {TreeItems.Count}, AllowForceUpdate = {AllowForceUpdate} }}";
}

public enum GitHubReferenceUpdate
{
    Updated,
    Created,
    ForceUpdated,
}

public readonly record struct GitHubCommitResult(string Sha, GitHubReferenceUpdate ReferenceUpdate);

public delegate Task<GitHubCommitResult> RunGitHubCommit(
    GitHubCommitSpec command,
    CancellationToken cancellationToken = default);

public static class GitHubCommitRunner
{
    public static async Task<GitHubCommitResult> RunAsync(
        GitHubCommitSpec command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = new GitHubClient(new ProductHeaderValue("CysharpActions"))
        {
            Credentials = new Credentials(command.Token)
        };

        var headSha = command.HeadSha ??
            (await client.Git.Reference.Get(command.Owner, command.Repository, $"heads/{command.Branch}")).Object.Sha;
        cancellationToken.ThrowIfCancellationRequested();

        var currentCommit = await client.Git.Commit.Get(command.Owner, command.Repository, headSha);
        var newTree = new NewTree { BaseTree = currentCommit.Tree.Sha };
        foreach (var item in command.TreeItems)
        {
            newTree.Tree.Add(new NewTreeItem
            {
                Path = item.Path,
                Mode = item.Mode,
                Type = TreeType.Blob,
                Sha = item.Delete ? null : default,
                Content = item.Delete ? null : item.Content,
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var treeResponse = await client.Git.Tree.Create(command.Owner, command.Repository, newTree);
        var createdCommit = await client.Git.Commit.Create(
            command.Owner,
            command.Repository,
            new NewCommit(command.Message, treeResponse.Sha, parents: [headSha]));

        try
        {
            await client.Git.Reference.Update(
                command.Owner,
                command.Repository,
                $"heads/{command.Branch}",
                new ReferenceUpdate(createdCommit.Sha));
            return new GitHubCommitResult(createdCommit.Sha, GitHubReferenceUpdate.Updated);
        }
        catch (ApiException ex) when (ex.Message.Contains("Reference does not exist", StringComparison.Ordinal))
        {
            await client.Git.Reference.Create(
                command.Owner,
                command.Repository,
                new NewReference($"refs/heads/{command.Branch}", createdCommit.Sha));
            return new GitHubCommitResult(createdCommit.Sha, GitHubReferenceUpdate.Created);
        }
        catch (ApiException ex) when (
            command.AllowForceUpdate &&
            ex.Message.Contains("Update is not a fast forward", StringComparison.Ordinal))
        {
            await client.Git.Reference.Update(
                command.Owner,
                command.Repository,
                $"heads/{command.Branch}",
                new ReferenceUpdate(createdCommit.Sha, force: true));
            return new GitHubCommitResult(createdCommit.Sha, GitHubReferenceUpdate.ForceUpdated);
        }
    }
}
