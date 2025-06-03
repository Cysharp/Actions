using Cysharp.Diagnostics;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;

namespace CysharpActions.Commands;

/// <summary>
/// GitHubのリリース情報を取得するためのインターフェース。
/// </summary>
public interface IGitHubRelaeseExe
{
    public Task<GitHubRelease[]> GetGitHubReleaseAsync();
}

/// <summary>
/// ghコマンドでGitHubのリリース情報を取得する実装。
/// </summary>
public class GitHubReleaseExeGh : IGitHubRelaeseExe
{
    public async Task<GitHubRelease[]> GetGitHubReleaseAsync()
    {
        // release_latest=$(gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest | jq -c -r ".[] | select(.isLatest == true) | .tagName")
        // sorted_latest=$(echo - e "${release_latest}\n${{ steps.trim.outputs.normalized_tag }}" | sort - V | tail - n 1)
        var releaseLatests = await "gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest";
        var githubReleases = JsonSerializer.Deserialize(releaseLatests, JsonSourceGenerationContext.Default.GitHubReleaseArray);
        return githubReleases ?? Array.Empty<GitHubRelease>();
    }
}

public class ValidateTagCommand(IGitHubRelaeseExe gitHubRelaeseExe)
{
    /// <summary>
    /// Normalize input tag. If the tag starts with 'v', it will be removed.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public string Normalize(string tag) => tag.StartsWith('v') ? tag[1..] : tag;

    /// <summary>
    /// Validate input tag. If the input tag is older than the latest release tag, it will return false. Otherwise, it will return true.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task ValidateTagAsync(string tag)
    {
        Env.useShell = false;

        if (string.IsNullOrEmpty(tag))
            throw new ActionCommandException($"Tag is invalid, emptry string is not allowed.");

        // tmporary skip validation on MagicOnion. There are no implementation for validation on each Major Version.
        if (GitHubContext.Current.Repository == "Cysharp/MagicOnion")
        {
            GitHubActions.WriteLog("Temporary skip validation on MagicOnion.");
            return;
        }

        try
        {
            var githubReleases = await gitHubRelaeseExe.GetGitHubReleaseAsync();
            var releaseTag = githubReleases?.SingleOrDefault(x => x.IsLatest)?.TagName;

            if (releaseTag is null)
            {
                // no release tag
                return;
            }
            else if (releaseTag == tag)
            {
                // input tag is same or newer than latest tag
                return;
            }

            var sortedLatest = new[] { releaseTag, tag }.OrderBy(x => x).Last();
            if (sortedLatest == tag)
            {
                // input tag is same or newer than latest tag
                return;
            }

            // input tag is older than latest tag, reverting!!
            throw new ActionCommandException($"Tag is invalid, reverting to old version. Please bump the version.");
        }
        catch (ProcessErrorException ex)
        {
            throw new ActionCommandException($"Failed to get latest release tag. {ex.Message}", ex);
        }
    }
}