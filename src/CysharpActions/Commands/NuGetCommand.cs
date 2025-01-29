using CysharpActions.Utils;

namespace CysharpActions.Commands;

public class NuGetCommand(string apiKey, bool dryRun)
{
    public async Task PushAsync(IEnumerable<string> nugetPaths)
    {
        foreach (var path in nugetPaths)
        {
            if (GlobFiles.IsGlobPattern(path))
            {
                // Is Wildcard?
                foreach (var file in GlobFiles.EnumerateFiles(path))
                {
                    await PushCoreAsync(file, apiKey, dryRun);
                }
            }
            else
            {
                // Is File?
                if (!File.Exists(path))
                    throw new ActionCommandException($"Asset file not found.", new FileNotFoundException(path));
                await PushCoreAsync(path, apiKey, dryRun);
            }
        }

        static async Task PushCoreAsync(string path, string apiKey, bool dryRun)
        {
            using var _ = new GitHubActionsGroup($"Uploading nuget. nugetPath: {path}");
            if (dryRun)
            {
                GitHubActions.WriteRawLog($"dotnet nuget push \"{EscapeArg(path)}\" --skip-duplicate -s https://api.nuget.org/v3/index.json -k {apiKey}");
            }
            else
            {
                await $"dotnet nuget push \"{EscapeArg(path)}\" --skip-duplicate -s https://api.nuget.org/v3/index.json -k {apiKey}";
            }
        }
    }
}
