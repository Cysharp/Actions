namespace Actions.Commands;

public class FileExsistsCommand(string pathPattern)
{
    public void Validate()
    {
        // do nothing for empty input
        if (string.IsNullOrWhiteSpace(pathPattern))
        {            
            throw new ActionCommandException("Input path was empty.");
        }

        // Handle glob path pattern.
        // /foo/bar/**/*
        // /foo/bar/*.txt
        if (ContainsGlobPattern(pathPattern))
        {
            if (!FileExistsWithGlob(pathPattern))
            {
                throw new ActionCommandException(pathPattern, new FileNotFoundException(pathPattern));
            }
            return;
        }

        // Specified full path pattern...
        // /foo/bar/piyo/poyo.txt
        if (!File.Exists(pathPattern))
        {
            throw new ActionCommandException(pathPattern, new FileNotFoundException(pathPattern));
        }
    }

    private static bool ContainsGlobPattern(string pattern) => pattern.Contains('*') || pattern.Contains('?');
    private static bool FileExistsWithGlob(string pattern)
    {
        try
        {
            pattern = pattern.Replace("\\", "/");
            string directory = GetParentDirectoryPath(pattern) ?? Directory.GetCurrentDirectory();
            string searchPattern = Path.GetFileName(pattern);

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
