using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CysharpActions.Commands;

public class ValidateTagCommand()
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

        // release_latest=$(gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest | jq -c -r ".[] | select(.isLatest == true) | .tagName")
        // sorted_latest=$(echo - e "${release_latest}\n${{ steps.trim.outputs.normalized_tag }}" | sort - V | tail - n 1)
        var releaseLatests = await "gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest";
        var githubReleases = JsonSerializer.Deserialize<GitHubRelease[]>(releaseLatests);
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

    private record GitHubRelease
    {
        [JsonPropertyName("tagName")]
        public required string TagName { get; init; }
        [JsonPropertyName("isLatest")]
        public required bool IsLatest { get; init; }
    }
}