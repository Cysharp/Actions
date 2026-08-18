using CysharpActions.Utils;

using CysharpActions.Runtime;

namespace CysharpActions.Commands;

public class NuGetCommand(string apiKey, bool dryRun, RunProcess? runProcess = null)
{
    private readonly RunProcess runProcess = runProcess ?? ProcessRunner.RunAsync;

    public async Task PushAsync(IEnumerable<string> nugetPaths, CancellationToken cancellationToken = default)
    {
        foreach (var path in nugetPaths)
        {
            if (GlobFiles.IsGlobPattern(path))
            {
                // Is Wildcard?
                foreach (var file in GlobFiles.EnumerateFiles(path))
                {
                    await PushCoreAsync(file, cancellationToken);
                }
            }
            else
            {
                // Is File?
                if (!File.Exists(path))
                    throw new ActionCommandException($"Asset file not found.", new FileNotFoundException(path));
                await PushCoreAsync(path, cancellationToken);
            }
        }

        async Task PushCoreAsync(string path, CancellationToken cancellationToken)
        {
            using var _ = GitHubActions.StartGroup($"Uploading nuget. nugetPath: {path}");
            var command = new CommandSpec(
                "dotnet",
                ["nuget", "push", path, "--skip-duplicate", "-s", "https://api.nuget.org/v3/index.json", "-k", apiKey],
                new HashSet<int> { 7 });
            if (dryRun)
            {
                ProcessRunner.WritePreview(command);
            }
            else
            {
                await runProcess(command, cancellationToken);
            }
        }
    }
}
