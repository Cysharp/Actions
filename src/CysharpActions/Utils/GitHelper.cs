using Cysharp.Diagnostics;
using CysharpActions.Contexts;

namespace CysharpActions.Utils;

public static class GitHelper
{
    /// <summary>
    /// Set git user.email/user.name if missing.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public static async Task SetGitUserEmailAsync(string email = "41898282+github-actions[bot]@users.noreply.github.com", string user = "github-actions[bot]")
    {
        var emailMissing = string.IsNullOrEmpty(await "git config --get user.email");
        if (emailMissing)
        {
            await $"git config --local user.email \"{email}\"";
        }
        var nameMissing= string.IsNullOrEmpty(await "git config --get user.name");
        if (nameMissing)
        {
            await $"git config --local user.name \"{user}\"";
        }
    }

    /// <summary>
    /// Set git remote url if missing.
    /// </summary>
    /// <returns></returns>
    public static async Task SetRemoteUrlIfMissingAsync()
    {
        try
        {
            var remote = await "git config --get remote.origin.url";
        }
        catch (ProcessErrorException)
        {
            GitHubActions.WriteLog($"git remote missing, settings remote ...");
            await $"git remote set-url origin \"https://github-actions:{GHEnv.Current.GH_TOKEN}@github.com/${GHEnv.Current.GH_REPO}\"";
        }
    }
}
