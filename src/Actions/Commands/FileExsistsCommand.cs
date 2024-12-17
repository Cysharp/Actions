using Actions.Utils;

namespace Actions.Commands;

public class FileExsistsCommand(string pathPattern, bool allowMissing = false)
{
    public void Validate()
    {
        // do nothing for empty input
        if (string.IsNullOrWhiteSpace(pathPattern))
            return;

        var pattern = GlobFiles.Normalize(pathPattern);

        // Handle glob path pattern.
        // /foo/bar/**/*
        // /foo/bar/*.txt
        if (GlobFiles.IsGlobPattern(pattern))
        {
            if (!GlobFiles.Exists(pattern))
            {
                // allow file not exists option
                if (allowMissing)
                    return;

                throw new ActionCommandException(pattern, new FileNotFoundException(pathPattern));
            }
            return;
        }

        // Specified full path pattern...
        // /foo/bar/piyo/poyo.txt
        if (!File.Exists(pattern))
        {
            throw new ActionCommandException(pattern, new FileNotFoundException(pathPattern));
        }
    }
}
