namespace Actions.Commands;

public class VersioningCommand(string tag, string prefix, VersionIncrement versionIncrement, bool isPrelease, string prerelease)
{
    /// <summary>
    /// Handling versioning
    /// </summary>
    /// <returns></returns>
    public string Versioning(bool withoutPrefix = false)
    {
        var version = GetNormalizedVersion(tag, prefix);
        var increment = IncrementVersion(version, versionIncrement);
        var format = FormatVersion(increment, prefix, isPrelease, prerelease, withoutPrefix);
        return format;
    }

    /// <summary>
    /// Normalize tag. Tag may contains prefix, remove prefix and retrun version.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    /// <exception cref="ActionCommandException"></exception>
    private static Version GetNormalizedVersion(string tag, string prefix)
    {
        if (string.IsNullOrEmpty(tag)) throw new ActionCommandException("tag missing");
        if (tag.StartsWith(prefix, StringComparison.Ordinal))
        {
            var span = tag.AsSpan();
            var substring = span[prefix.Length..];
            return Version.Parse(substring);
        }
        else
        {
            return Version.Parse(tag);
        }
    }

    /// <summary>
    /// Increment version for specific place
    /// </summary>
    /// <param name="version"></param>
    /// <param name="versionIncrement"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private static Version IncrementVersion(Version version, VersionIncrement versionIncrement) => versionIncrement switch
    {
        VersionIncrement.Major => new Version(version.Major + 1, version.Minor, version.Build),
        VersionIncrement.Minor => new Version(version.Major, version.Minor + 1, version.Build),
        VersionIncrement.Patch => new Version(version.Major, version.Minor, version.Build + 1),
        _ => throw new NotImplementedException(nameof(versionIncrement)),
    };

    /// <summary>
    /// Format version
    /// </summary>
    /// <param name="version"></param>
    /// <param name="prefix"></param>
    /// <param name="isPrelease"></param>
    /// <param name="prerelease"></param>
    /// <returns></returns>
    private static string FormatVersion(Version version, string prefix, bool isPrelease, string prerelease, bool withoutPrefix)
    {
        var preReleaseSuffix = isPrelease && prerelease != "" ? $"-{prerelease}" : "";
        return withoutPrefix ? $"{version}{preReleaseSuffix}" : $"{prefix}{version}{preReleaseSuffix}";
    }
}
