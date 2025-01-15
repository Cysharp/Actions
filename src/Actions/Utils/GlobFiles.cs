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
        var directory = GetParentDirectoryPath(pattern) ?? Directory.GetCurrentDirectory();
        var searchPattern = Path.GetFileName(pattern);

        if (pattern.Contains("/**/"))
        {
            return Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories);
        }
        if (pattern.Contains("*/"))
        {
            return EnumerateFileWildcard(pattern, searchPattern, SearchOption.AllDirectories);
        }
        else
        {
            return Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
        }
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
    /// Normalize \ to /
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static string Normalize(string pattern) => pattern.Replace("\\", "/");

    private static string GetParentDirectoryPath(string path)
    {
        var limit = 5;
        var current = 0;
        var dir = path;
        bool next;
        do
        {
            if (current >= limit)
                throw new ActionCommandException($"Recursively approaced parent directory but reached limit. {current}/{limit}");

            dir = Path.GetDirectoryName(dir) ?? "";
            var fileName = Path.GetFileName(dir);
            next = fileName == "**" || fileName == "*";
            current++;
        } while (next);

        return dir;
    }

    private static IEnumerable<string> EnumerateFileWildcard(string path, string searchPattern, SearchOption searchOption)
    {
        var fileName = Path.GetFileName(path);
        if (fileName == "*")
        {
            return Directory.GetDirectories(Path.GetDirectoryName(path)!).SelectMany(x => Directory.GetFiles(x, searchPattern, searchOption));
        }
        else
        {
            return EnumerateFileWildcard(Path.GetDirectoryName(path)!, searchPattern, searchOption);
        }
    }

}
