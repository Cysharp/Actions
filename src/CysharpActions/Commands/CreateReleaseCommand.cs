using CysharpActions.Contexts;
using CysharpActions.Utils;

using CysharpActions.Runtime;

namespace CysharpActions.Commands;

public class CreateReleaseCommand(string tag, string releaseTitle, RunProcess? runProcess = null)
{
    private readonly RunProcess runProcess = runProcess ?? ProcessRunner.RunAsync;

    /// <summary>
    /// Create GitHub Release
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ActionCommandException"></exception>
    public async Task CreateReleaseAsync(CancellationToken cancellationToken = default)
    {
        GitHubActions.WriteLog($"Set git user.email/user.name if missing ...");
        await GitHelper.SetGitUserEmailAsync(runProcess: runProcess, cancellationToken: cancellationToken);
        await runProcess(new CommandSpec("git", ["config", "-l"]), cancellationToken);

        // git tag
        using (_ = GitHubActions.StartGroup("Create git tag, if not exists"))
        {
            var tags = await runProcess(new CommandSpec("git", ["ls-remote", "--tags"]), cancellationToken);
            if (!tags.OutputLines.Any(x => x.EndsWith($"refs/tags/{tag}")))
            {
                GitHubActions.WriteLog("git tag not found. Begin tag and push to origin.");
                await runProcess(new CommandSpec("git", ["tag", tag]), cancellationToken);
                await runProcess(new CommandSpec("git", ["push", "origin", tag]), cancellationToken);
            }
        }

        // create release
        using (_ = GitHubActions.StartGroup("Create Release"))
        {
            await runProcess(new CommandSpec("gh", ["release", "create", tag, "--draft", "--verify-tag", "--title", releaseTitle, "--generate-notes"]), cancellationToken);
            // wait a while
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    /// <summary>
    /// Upload asset files to the release
    /// </summary>
    /// <param name="assetPaths"></param>
    /// <returns></returns>
    public async Task UploadAssetFilesAsync(IEnumerable<string> assetPaths, CancellationToken cancellationToken = default)
    {
        foreach (var path in assetPaths)
        {
            if (GlobFiles.IsGlobPattern(path))
            {
                // Is Wildcard?
                foreach (var file in GlobFiles.EnumerateFiles(path))
                {
                    await UploadCoreAsync(file, cancellationToken);
                }
            }
            else
            {
                // Is File?
                if (!File.Exists(path))
                    throw new ActionCommandException($"Asset file not found.", new FileNotFoundException(path));

                await UploadCoreAsync(path, cancellationToken);
            }
        }

        async Task UploadCoreAsync(string path, CancellationToken cancellationToken)
        {
            using var _ = GitHubActions.StartGroup($"Uploading asset. tag: {tag}. assetPath: {path}");
            await runProcess(new CommandSpec("gh", ["release", "upload", tag, path]), cancellationToken);
        }
    }
}
