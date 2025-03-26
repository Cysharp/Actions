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
        try
        {
            var remote = await "git config --get remote.origin.url";
        }
        catch (ProcessErrorException)
        {
            await $"git remote set-url origin \"https://github-actions:{GHEnv.Current.GH_TOKEN}@github.com/${GHEnv.Current.GH_REPO}\"";
        }

        try
        {
            await "git config --get user.email";
        }
        catch (ProcessErrorException)
        {
            await $"git config --local user.email \"{email}\"";
        }

        try
        {
            await "git config --get user.name";
        }
        catch (ProcessErrorException)
        {
            await $"git config --local user.name \"{user}\"";
        }
    }
}
