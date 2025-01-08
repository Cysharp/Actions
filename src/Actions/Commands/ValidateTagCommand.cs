using System.Text.Json;
using System.Text.Json.Serialization;

namespace Actions.Commands;

public class ValidateTagCommand()
{
    /// <summary>
    /// Normalize input tag. If the tag starts with 'v', it will be removed.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public string Normalize(string tag) => tag.StartsWith("v") ? tag.Substring(1) : tag;

    /// <summary>
    /// Validate input tag. If the input tag is older than the latest release tag, it will return false. Otherwise, it will return true.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task<(bool result, string releaseTag)> ValidateTagAsync(string tag)
    {
        // release_latest=$(gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest | jq -c -r ".[] | select(.isLatest == true) | .tagName")
        // sorted_latest=$(echo - e "${release_latest}\n${{ steps.trim.outputs.normalized_tag }}" | sort - V | tail - n 1)
        var releaseLatests = await "gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest";
        var githubReleases = JsonSerializer.Deserialize<GitHubRelease[]>(releaseLatests);
        var releaseTag = githubReleases?.SingleOrDefault(x => x.IsLatest)?.TagName;

        if (releaseTag == null)
        {
            // no release tag
            return (true, "");
        }

        var sortedLatest = new[] { releaseTag, tag }.OrderBy(x => x).Last();
        if (sortedLatest == tag)
        {
            // input tag is same or newer than latest tag
            return (true, releaseTag);
        }

        // input tag is older than latest tag, reverting!!
        return (false, releaseTag);
    }

    private record GitHubRelease
    {
        [JsonPropertyName("tagName")]
        public required string TagName { get; init; }
        [JsonPropertyName("isLatest")]
        public required bool IsLatest { get; init; }
    }
}