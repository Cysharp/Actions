namespace Actions.Utils;

public static class GlobFiles
{
    /// <summary>
    /// Check if pattern is glob pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static bool IsGlobPattern(string pattern) => pattern.Contains('*') || pattern.Contains('?');

    /// <summary>
    /// Enumerate files with glob pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static IEnumerable<string> EnumerateFiles(string pattern)
    {
        var (rootDirectory, includePattern) = GetGlobRootAndInputPattern(pattern);
        var files = new Microsoft.Extensions.FileSystemGlobbing.Matcher()
          .AddInclude(includePattern)
          .Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(rootDirectory)))
          .Files
          .Select(x => Path.Combine(rootDirectory, x.Path));
        return files;
    }

    /// <summary>
    /// Check if file exists with glob pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static bool Exists(string pattern)
    {
        try
        {
            return EnumerateFiles(pattern).Any();
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Get glob root directory and input pattern
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    private static (string rootDirectory, string includePattern) GetGlobRootAndInputPattern(string pattern)
    {
        var normalizedPattern = NormalizePath(pattern);
        var splitted = normalizedPattern.Split('/', StringSplitOptions.TrimEntries).AsSpan();
        var indexOfRoot = 0;
        foreach (var item in splitted)
        {
            if (item.Contains('*')) // Microsoft.Extensions.FileSystemGlobbing not accept ? for glob pattern.
                break;
            indexOfRoot++;
        }
        var rootDirectory = Path.Combine(splitted[..indexOfRoot]);
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        var includePattern = normalizedPattern[rootDirectory.Length..];
        return (fullRootDirectory, includePattern);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
