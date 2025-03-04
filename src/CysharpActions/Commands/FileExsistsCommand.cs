using CysharpActions.Utils;

namespace CysharpActions.Commands;

public class FileExsistsCommand()
{
    public void ValidateAssetPath(IEnumerable<string> pathPatterns)
    {
        foreach (var path in pathPatterns)
        {
            using var _ = GitHubActions.StartGroup($"Validating path, {path}");
            {
                GitHubActions.WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(path)}");
                ValidateCore(path, false);
            }
        }
    }

    public void ValidateNuGetPath(IEnumerable<string> pathPatterns)
    {
        foreach (var pathPattern in pathPatterns)
        {
            using var _ = GitHubActions.StartGroup($"Validating path, {pathPattern}");
            {
                GitHubActions.WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(pathPattern)}");

                var fileName = Path.GetFileName(pathPattern);
                var extension = Path.GetExtension(fileName);
                var allowMissing = extension == ".snupkg";

                ValidateCore(pathPattern, allowMissing);
            }
        }
    }

    private static void ValidateCore(string pattern, bool allowMissing)
    {
        // do nothing for empty input
        if (string.IsNullOrWhiteSpace(pattern))
        {
            GitHubActions.WriteLog("Empty path detected, skip execution.");
            return;
        }

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
            {
                GitHubActions.WriteLog($"Missing file detected, it is allowed. pattern: {pattern}");
                return;
            }

            throw new ActionCommandException(pattern, new FileNotFoundException(pattern));
        }
    }
}
