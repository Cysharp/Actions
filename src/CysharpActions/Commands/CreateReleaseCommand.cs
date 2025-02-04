using CysharpActions.Utils;

namespace CysharpActions.Commands;

public class CreateReleaseCommand(string tag, string releaseTitle)
{
    /// <summary>
    /// Create GitHub Release
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ActionCommandException"></exception>
    public async Task CreateReleaseAsync()
    {
        Env.useShell = false;

        // git tag
        using (_ = new GitHubActionsGroup("Create git tag"))
        {
            await $"git tag {tag}";
            await $"git push origin {tag}";
        }

        // create release
        using (_ = new GitHubActionsGroup("Create Release"))
        {
            await $"gh release create {tag} --draft --verify-tag --title \"{releaseTitle}\" --generate-notes";
            // wait a while
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Upload asset files to the release
    /// </summary>
    /// <param name="assetPaths"></param>
    /// <returns></returns>
    public async Task UploadAssetFilesAsync(IEnumerable<string> assetPaths)
    {
        Env.useShell = false;

        foreach (var path in assetPaths)
        {
            if (GlobFiles.IsGlobPattern(path))
            {
                // Is Wildcard?
                foreach (var file in GlobFiles.EnumerateFiles(path))
                {
                    await UploadCoreAsync(tag, file);
                }
            }
            else
            {
                // Is File?
                if (!File.Exists(path))
                    throw new ActionCommandException($"Asset file not found.", new FileNotFoundException(path));

                await UploadCoreAsync(tag, path);
            }
        }

        static async Task UploadCoreAsync(string tag, string path)
        {
            using var _ = new GitHubActionsGroup($"Uploading asset. tag: {tag}. assetPath: {path}");
            await $"gh release upload {tag} \"{path}\"";
        }
    }
}