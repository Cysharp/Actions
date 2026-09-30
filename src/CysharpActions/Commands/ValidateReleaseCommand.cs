using CysharpActions.Runtime;
using NuGet.Versioning;
using System.IO.Compression;
using System.Xml.Linq;

namespace CysharpActions.Commands;

/// <summary>Validates release inputs without changing the legacy validate-tag contract.</summary>
public sealed class ValidateReleaseCommand(IGitHubReleaseExe releases, RunProcess? runProcess = null)
{
    private readonly RunProcess run = runProcess ?? ProcessRunner.RunAsync;

    /// <summary>Checks Git syntax and NuGet version ordering, returning the version without a v prefix.</summary>
    public async Task<string> ValidateAsync(string tag, RepositoryContext repository, CancellationToken cancellationToken = default)
    {
        var version = ParseTag(tag);
        await run(new CommandSpec("git", ["check-ref-format", $"refs/tags/{tag}"]), cancellationToken);
        // MagicOnion supports multiple major release lines; only skip ordering.
        if (repository.Repository != "Cysharp/MagicOnion")
        {
            var latest = (await releases.GetGitHubReleaseAsync(cancellationToken)).SingleOrDefault(x => x.IsLatest);
            if (latest is not null && VersionComparer.VersionRelease.Compare(version, ParseTag(latest.TagName)) < 0)
                throw new ActionCommandException("Tag is older than the latest release. Please bump the version.");
        }
        return tag.StartsWith('v') ? tag[1..] : tag;
    }

    /// <summary>Parses a package version, allowing a single leading v on Git tags.</summary>
    public static NuGetVersion ParseTag(string tag)
    {
        var value = tag.StartsWith('v') ? tag[1..] : tag;
        if (tag.Any(char.IsWhiteSpace) || !NuGetVersion.TryParse(value, out var version))
            throw new ActionCommandException($"Invalid release version: '{tag}'.");
        return version;
    }

    /// <summary>Validates every package before authentication or publication; metadata is ignored as on NuGet.</summary>
    public static void ValidatePackages(string directory, string version)
    {
        var expected = ParseTag(version);
        var packages = Directory.GetFiles(directory, "*.nupkg");
        if (packages.Length == 0)
            throw new ActionCommandException("No NuGet packages were found.");
        foreach (var path in packages.Concat(Directory.GetFiles(directory, "*.snupkg")))
        {
            using var archive = ZipFile.OpenRead(path);
            var specs = archive.Entries.Where(x => !x.FullName.Contains('/') && x.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (specs.Length != 1)
                throw new ActionCommandException($"Expected one root nuspec in '{path}'.");
            using var stream = specs[0].Open();
            var root = XDocument.Load(stream).Root;
            var ns = root?.Name.Namespace ?? XNamespace.None;
            var actual = root?.Element(ns + "metadata")?.Element(ns + "version")?.Value;
            if (!NuGetVersion.TryParse(actual, out var parsed) || !VersionComparer.VersionRelease.Equals(expected, parsed))
                throw new ActionCommandException($"Package version mismatch in '{path}': expected '{version}', got '{actual}'.");
        }
    }
}
