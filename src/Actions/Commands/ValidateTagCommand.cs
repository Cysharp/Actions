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
    public async Task<(bool validated, ValidateTagResult result, string releaseTag)> ValidateTagAsync(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return (false, ValidateTagResult.InvalidMissingTag, "");

        // release_latest=$(gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest | jq -c -r ".[] | select(.isLatest == true) | .tagName")
        // sorted_latest=$(echo - e "${release_latest}\n${{ steps.trim.outputs.normalized_tag }}" | sort - V | tail - n 1)
        var releaseLatests = await "gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest";
        var githubReleases = JsonSerializer.Deserialize<GitHubRelease[]>(releaseLatests);
        var releaseTag = githubReleases?.SingleOrDefault(x => x.IsLatest)?.TagName;

        if (releaseTag is null)
        {
            // no release tag
            return (true, ValidateTagResult.ValidReleaseTagNotfound, "");
        }
        else if (releaseTag == tag)
        {
            // input tag is same or newer than latest tag
            return (true, ValidateTagResult.ValidVersionSame, releaseTag);
        }

        var sortedLatest = new[] { releaseTag, tag }.OrderBy(x => x).Last();
        if (sortedLatest == tag)
        {
            // input tag is same or newer than latest tag
            return (true, ValidateTagResult.ValidVersionNewer, releaseTag);
        }

        // input tag is older than latest tag, reverting!!
        return (false, ValidateTagResult.InvalidReverting, releaseTag);
    }

    private record GitHubRelease
    {
        [JsonPropertyName("tagName")]
        public required string TagName { get; init; }
        [JsonPropertyName("isLatest")]
        public required bool IsLatest { get; init; }
    }
}

public enum ValidateTagResult
{
    /// <summary>
    /// There are no release tags.
    /// </summary>
    ValidReleaseTagNotfound,
    /// <summary>
    /// Validation Success, version is newer than current.
    /// </summary>
    ValidVersionNewer,
    /// <summary>
    /// Validation Success, version is same as current.
    /// </summary>
    ValidVersionSame,
    /// <summary>
    /// Validation Failed, input tag is missing.
    /// </summary>
    InvalidMissingTag,
    /// <summary>
    /// Validation Failed, input tag is reverting.
    /// </summary>
    InvalidReverting,
}

public static class ValidateTagResultExtensions
{
    public static string ToReason(this ValidateTagResult value) => value switch
    {
        ValidateTagResult.InvalidMissingTag => $"Tag is invalid, emptry string is not allowed.",
        ValidateTagResult.InvalidReverting => $"Tag is invalid, reverting to old version. Please bump the version.",
        ValidateTagResult.ValidReleaseTagNotfound => $"Tag is valid, allow tag because release tag not found.",
        ValidateTagResult.ValidVersionNewer => "Tag is valid, newer than current release tag.",
        ValidateTagResult.ValidVersionSame => $"Tag is valid, same as current release tag.",
        _ => throw new NotImplementedException(value.ToString()),
    };

    public static int ToExitCode(this ValidateTagResult value) => value switch
    {
        ValidateTagResult.InvalidMissingTag => 1,
        ValidateTagResult.InvalidReverting => 1,
        ValidateTagResult.ValidReleaseTagNotfound => 0,
        ValidateTagResult.ValidVersionNewer => 0,
        ValidateTagResult.ValidVersionSame => 0,
        _ => throw new NotImplementedException(value.ToString()),
    };
}