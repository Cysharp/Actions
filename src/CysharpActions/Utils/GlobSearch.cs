namespace CysharpActions.Utils;

public static class GlobSearch
{
    /// <summary>
    /// Check if pattern is glob pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static bool IsGlobPattern(string pattern) => pattern.Contains('*');  // Not supporting ?

    /// <summary>
    /// Enumerate files with glob pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static IEnumerable<string> EnumerateFiles(string pattern)
    {
        var (rootDirectory, includePattern) = GetGlobRootAndInputPattern(pattern);
        IEnumerable<string> files = new Microsoft.Extensions.FileSystemGlobbing.Matcher()
          .AddInclude(includePattern)
          .Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(rootDirectory)))
          .Files
          .Select(x => Path.Combine(rootDirectory, x.Path)); // Matcher returns relative path from root directory.
        return files;
    }

    /// <summary>
    /// Check if file exists with glob pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static bool Exists(string pattern)
    {
        return EnumerateFiles(pattern).Any();
    }

    /// <summary>
    /// Get glob root directory and input pattern
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    private static (string rootDirectory, string includePattern) GetGlobRootAndInputPattern(string pattern)
    {
        var normalizedPattern = Path.GetFullPath(pattern).Replace('\\', '/');
        var splitted = normalizedPattern.Split('/', StringSplitOptions.TrimEntries).AsSpan();
        var indexOfRoot = 0;
        foreach (var item in splitted)
        {
            if (IsGlobPattern(item))
                break;
            indexOfRoot++;
        }
        // Its not glob pattern
        if (indexOfRoot == splitted.Length)
            return (Path.GetDirectoryName(normalizedPattern) ?? Directory.GetCurrentDirectory(), Path.GetFileName(pattern));

        // Non-Windows root directory may become emptry string
        var rootMarker = normalizedPattern[0] == '/' ? "/" : "";
        var rootDirectory = rootMarker + Path.Combine(splitted[..indexOfRoot]);
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        var includePattern = normalizedPattern[(rootDirectory.Length + rootMarker.Length)..];
        return (fullRootDirectory, includePattern);
    }
}
