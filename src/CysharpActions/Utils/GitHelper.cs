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
        string email = "41898282+github-actions[bot]@users.noreply.github.com",
        string user = "github-actions[bot]",
        RunProcess? runProcess = null,
        CancellationToken cancellationToken = default)
    {
        runProcess ??= ProcessRunner.RunAsync;
        try
        {
            GHEnv.Current.Validate();
        }
        catch (ArgumentNullException ex)
        {
            throw new ActionCommandException("GH_REPO and GH_TOKEN is required, but not set.", ex);
        }

        try
        {
            var remoteUrl = $"https://github-actions:{GHEnv.Current.GH_TOKEN}@github.com/{GHEnv.Current.GH_REPO}";
            var remote = await runProcess(new CommandSpec("git", ["config", "--get", "remote.origin.url"]), cancellationToken);
            if (remote.Stdout != remoteUrl)
            {
                await runProcess(new CommandSpec("git", ["remote", "set-url", "origin", remoteUrl], new HashSet<int> { 3 }), cancellationToken);
            }
        }
        catch (ProcessErrorException)
        {
            var remoteUrl = $"https://github-actions:{GHEnv.Current.GH_TOKEN}@github.com/{GHEnv.Current.GH_REPO}";
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
