﻿using CysharpActions.Contexts;
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

        // set remote
        var remote = await "git config --get remote.origin.url";
        if (string.IsNullOrEmpty(remote))
        {
            using (_ = GitHubActions.StartGroup("Setting git remote"))
            {
                await $"git remote set-url origin \"https://github-actions:{GHEnv.Current.GH_TOKEN}@github.com/${GHEnv.Current.GH_REPO}\"";
                await $"git config --local user.email \"41898282+github-actions[bot]@users.noreply.github.com\"";
                await $"git config --local user.name \"github-actions[bot]\"";
            }
        }

        // git tag
        using (_ = GitHubActions.StartGroup("Create git tag, if not exists"))
        {
            var tags = await $"git ls-remote --tags";
            if (!tags.ToMultiLine().Any(x => x.EndsWith($"refs/tags/{tag}")))
            {
                GitHubActions.WriteLog("git tag not found. Begin tag and push to origin.");
                await $"git tag {tag}";
                await $"git push origin {tag}";
            }
        }

        // create release
        using (_ = GitHubActions.StartGroup("Create Release"))
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
            using var _ = GitHubActions.StartGroup($"Uploading asset. tag: {tag}. assetPath: {path}");
            await $"gh release upload {tag} \"{path}\"";
        }
    }
}
