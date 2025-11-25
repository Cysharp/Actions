namespace CysharpActions.Commands;

public enum VersionIncrement
{
    Major,
    Minor,
    Patch,
}

public class VersioningCommand(string prefix = "", string suffix = "")
{
    /// <summary>
    /// Handling versioning
    /// </summary>
    /// <returns></returns>
    public string UpdateVersion(string tag, VersionIncrement versionIncrement)
    {
        // 対応状況:
        // 〇: 既存のprefixやsuffixを維持する (v1.0.0-alpha.1 -> v1.0.1-alpha.1 など)
        // 〇: prefixがない場合に追加する (1.0.0 -> v1.0.1 など)
        // 〇: suffixがない場合に追加する (1.0.0 -> 1.0.1-alpha.1 など)
        // ×: prefixを変更する (v1.0.0 -> Ver1.0.0 など)
        // ×: suffixを変更する (1.0.0-alpha.1 -> 1.0.0-alpha.2 など)

        var versionRaw = GetNormalizedVersion(tag, prefix, suffix);
        var incremented = IncrementVersion(versionRaw, versionIncrement);
        var newVersion = $"{prefix}{incremented}{suffix}";
        return newVersion;
    }

    /// <summary>
    /// Normalize tag to x.y.z format.
    /// If version has prefix, remove it. If version has suffix, remove it.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="prefix"></param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    /// <exception cref="ActionCommandException"></exception>
    private static Version GetNormalizedVersion(string tag, string prefix, string suffix)
    {
        var tagSpan = tag.AsSpan();
        Span<char> destination = stackalloc char[tagSpan.Length];
        tagSpan.CopyTo(destination);

        if (string.IsNullOrEmpty(tag)) throw new ActionCommandException("tag missing");
        if (tagSpan.StartsWith(prefix.AsSpan(), StringComparison.Ordinal))
        {
            tagSpan[prefix.Length..].CopyTo(destination);
            destination = destination[..(tagSpan.Length - prefix.Length)];
        }

        if (tagSpan.EndsWith(suffix.AsSpan(), StringComparison.Ordinal))
        {
            destination[..^suffix.Length].CopyTo(destination);
            destination = destination[..(destination.Length - suffix.Length)];
        }

        return Version.Parse(destination);
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
}
