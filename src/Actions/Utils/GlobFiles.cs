namespace Actions.Utils;

public static class GlobFiles
{
    public static bool IsGlobPattern(string pattern) => pattern.Contains('*') || pattern.Contains('?');
    public static bool Exists(string pattern)
    {
        try
        {
            var directory = GetParentDirectoryPath(pattern) ?? Directory.GetCurrentDirectory();
            var searchPattern = Path.GetFileName(pattern);

            if (pattern.Contains("/**/"))
            {
                return Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories).Any();
            }
            if (pattern.Contains("*/"))
            {
                return EnumerateFileWildcard(pattern, searchPattern, SearchOption.AllDirectories).Any();
            }
            else
            {
                return Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly).Any();
            }
        }
        catch (Exception ex)
        {
            throw new ActionCommandException($"Error happen on checking file. Is specified path correct? path: {pattern}", ex);
        }
    }

    /// <summary>
    /// Normalize \r\n to \n
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static string Normalize(string pattern) => pattern.Replace("\\", "/");

    private static string GetParentDirectoryPath(string path)
    {
        var limit = 5;
        var current = 0;
        var dir = path;
        var fileName = Path.GetFileName(dir);
        var next = false;
        do
        {
            dir = Path.GetDirectoryName(dir) ?? "";
            fileName = Path.GetFileName(dir);
            current++;
            if (current >= limit)
            {
                throw new ActionCommandException($"Recursively approaced parent directory but reached limit. {current}/{limit}");
            }
            next = fileName == "**" || fileName == "*";
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
