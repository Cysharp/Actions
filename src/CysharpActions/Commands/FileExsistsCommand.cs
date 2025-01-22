using CysharpActions.Utils;

namespace CysharpActions.Commands;

public class FileExsistsCommand(bool allowMissing = false)
{
    public void Validate(string pattern)
    {
        // do nothing for empty input
        if (string.IsNullOrWhiteSpace(pattern))
            return;

        if (GlobFiles.IsGlobPattern(pattern))
        {
            // Handle glob path pattern.
            // /foo/bar/**/*
            // /foo/bar/*.txt

            // file found
            if (GlobFiles.Exists(pattern))
                return;
            // allow file not exists
            if (allowMissing)
                return;

            throw new ActionCommandException(pattern, new FileNotFoundException(pattern));
        }
        else
        {
            // Handle full path pattern...
            // /foo/bar/piyo/poyo.txt

            // file found
            if (File.Exists(pattern))
                return;
            // allow file not exists
            if (allowMissing)
                return;

            throw new ActionCommandException(pattern, new FileNotFoundException(pattern));
        }
    }
}
