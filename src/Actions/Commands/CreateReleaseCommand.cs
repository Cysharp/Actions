using Actions.Utils;
using static Actions.Utils.ZxHelper;

namespace Actions.Commands;

public class CreateReleaseCommand(string tag, string releaseTitle)
{
    /// <summary>
    /// Create GitHub Release
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ActionCommandException"></exception>
    public async Task CreateReleaseAsync()
    {
        // git tag
        using (_ = new GitHubActionsGroupLogger("Create git tag"))
        {
            await $"git tag {tag}";
            await $"git push origin {tag}";
        }

        // create release
        using (_ = new GitHubActionsGroupLogger("Create Release"))
        {
            await $"gh release create {tag} --draft --verify-tag --title \"{releaseTitle}\" --generate-notes";
            // wait a while
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Upload asset files to the release
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="assetPaths"></param>
    /// <returns></returns>
    public async Task UploadAssetFiles(string[] assetPaths)
    {
        foreach (var assetPath in assetPaths)
        {
            if (GlobFiles.IsGlobPattern(assetPath))
            {
                // Is Wildcard?
                foreach (var file in GlobFiles.EnumerateFiles(assetPath))
                {
                    using var _ = new GitHubActionsGroupLogger($"Uploading asset. tag: {tag}. assetPath: {file}");
                    await $"gh release upload {tag} \"{EscapeArg(file)}\"";
                }
            }
            else
            {
                // Is File?
                using var _ = new GitHubActionsGroupLogger($"Uploading asset. tag: {tag}. assetPath: {assetPath}");
                await $"gh release upload {tag} \"{EscapeArg(assetPath)}\"";
            }
        }
    }
}
