using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<ActionsCommand>();
app.Run(args);

public class ActionsCommand
{
    /// <summary>Get version string from tag</summary>
    /// <param name="tag"></param>
    /// <param name="prefix"></param>
    /// <param name="versionIncrement"></param>
    /// <param name="isPrelease"></param>
    /// <param name="prerelease"></param>
    /// <param name="outputFormat"></param>
    public void Versioning(string tag, string prefix = "", VersionIncrement versionIncrement = VersionIncrement.Patch, bool isPrelease = false, string prerelease = "preview", OutputFormat outputFormat = global::OutputFormat.Console)
    {
        var handler = new VersionHandler(tag, prefix, versionIncrement, isPrelease, prerelease);
        var versioning = handler.Versioning();

        Console.WriteLine(OutputFormat("version", versioning, outputFormat));
    }

    private string OutputFormat(string key, string value, OutputFormat format) => format switch
    {
        global::OutputFormat.Console => value,
        global::OutputFormat.GitHubActions => $"{key}={value}",
        _ => throw new NotImplementedException(nameof(format)),
    };
}

public class VersionHandler(string tag, string prefix, VersionIncrement versionIncrement, bool isPrelease, string prerelease)
{
    /// <summary>
    /// Handling versioning
    /// </summary>
    /// <returns></returns>
    public string Versioning()
    {
        var version = GetNormalizedVersion(tag, prefix);
        var increment = IncrementVersion(version, versionIncrement);
        var format = FormatVersion(increment, prefix, isPrelease, prerelease);
        return format;
    }

    /// <summary>
    /// Normalize tag. Tag may contains prefix, remove prefix and retrun version.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    /// <exception cref="ActionCommandException"></exception>
    private Version GetNormalizedVersion(string tag, string prefix)
    {
        if (string.IsNullOrEmpty(tag)) throw new ActionCommandException("tag missing");
        if (tag.StartsWith(prefix, StringComparison.Ordinal))
        {
            var span = tag.AsSpan();
            var substring = span.Slice(prefix.Length, span.Length - prefix.Length);
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
    private Version IncrementVersion(Version version, VersionIncrement versionIncrement) => versionIncrement switch
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
    private string FormatVersion(Version version, string prefix, bool isPrelease, string prerelease)
    {
        var preReleaseSuffix = isPrelease ? $"-{prerelease}" : "";
        return $"{prefix}{version}{preReleaseSuffix}";
    }
}

public enum VersionIncrement
{
    Major,
    Minor,
    Patch,
    //Prerelease, // TODO: how to calculate count since last tag?
}

public enum OutputFormat
{
    Console,
    GitHubActions,
}
public class ActionCommandException : Exception
{
    public ActionCommandException(string message) : base(message) { }
}
