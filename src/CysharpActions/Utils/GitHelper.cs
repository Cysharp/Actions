using Cysharp.Diagnostics;
using CysharpActions.Contexts;

using CysharpActions.Runtime;

namespace CysharpActions.Utils;

public static class GitHelper
{
    /// <summary>
    /// Set git user.email/user.name if missing.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public static async Task SetGitUserEmailAsync(
        GitHubCredentials credentials,
        string email = "41898282+github-actions[bot]@users.noreply.github.com",
        string user = "github-actions[bot]",
        RunProcess? runProcess = null,
        CancellationToken cancellationToken = default)
    {
        runProcess ??= ProcessRunner.RunAsync;
        credentials.Validate();
        var token = credentials.RequireToken();
        var repository = RepositoryContext.Required(credentials.Repository, "GH_REPO");

        try
        {
            var remoteUrl = $"https://github-actions:{token}@github.com/{repository}";
            var remote = await runProcess(new CommandSpec("git", ["config", "--get", "remote.origin.url"]), cancellationToken);
            if (remote.Stdout != remoteUrl)
            {
                await runProcess(new CommandSpec("git", ["remote", "set-url", "origin", remoteUrl], new HashSet<int> { 3 }), cancellationToken);
            }
        }
        catch (ProcessErrorException)
        {
            var remoteUrl = $"https://github-actions:{token}@github.com/{repository}";
            await runProcess(new CommandSpec("git", ["remote", "set-url", "origin", remoteUrl], new HashSet<int> { 3 }), cancellationToken);
        }

        try
        {
            await runProcess(new CommandSpec("git", ["config", "--get", "user.email"]), cancellationToken);
        }
        catch (ProcessErrorException)
        {
            await runProcess(new CommandSpec("git", ["config", "--local", "user.email", email]), cancellationToken);
        }

        try
        {
            await runProcess(new CommandSpec("git", ["config", "--get", "user.name"]), cancellationToken);
        }
        catch (ProcessErrorException)
        {
            await runProcess(new CommandSpec("git", ["config", "--local", "user.name", user]), cancellationToken);
        }
    }
}
