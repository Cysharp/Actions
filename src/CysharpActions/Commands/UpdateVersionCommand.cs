using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;

namespace CysharpActions.Commands;

public sealed class UpdateVersionCommand
{
    private readonly string version;

    public UpdateVersionCommand(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ActionCommandException("Version must not be empty.");
        this.version = version;
    }

    public void Execute(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // Build every edit before writing the first file. A malformed later file must not
        // leave earlier files updated.
        var edits = new List<PendingEdit>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                GitHubActions.WriteLog("Empty path detected, skip execution.");
                continue;
            }
            if (!File.Exists(path))
                throw new FileNotFoundException("Version file not found.", path);

            GitHubActions.WriteLog($"Update begin, {path} ...");
            var before = File.ReadAllText(path);
            using (_ = GitHubActions.StartGroup($"Before, {path}"))
                GitHubActions.WriteLog(before);

            var after = UpdateContents(Path.GetFileName(path), before, version);
            edits.Add(new PendingEdit(path, after));
        }

        foreach (var edit in edits)
        {
            File.WriteAllText(edit.Path, edit.Contents);
            using var _ = GitHubActions.StartGroup($"After, {edit.Path}");
            GitHubActions.WriteLog(edit.Contents);
        }

    }

    internal static string UpdateContents(string fileName, string contents, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        return fileName switch
        {
            // UPM
            "package.json" => UpdateUpm(contents, version),
            // Godot
            "plugin.cfg" => UpdateGodot(contents, version),
            // .NET
            "Directory.Build.props" => UpdateDirectoryBuildProps(contents, version),
            // Other
            _ => throw new ActionCommandException($"Unsupported version file '{fileName}'."),
        };
    }

    private static string UpdateUpm(string contents, string version)
    {
        var (_, after) = RegrexReplace.Replace(contents, @"""version"":\s*""(.*?)""", $@"""version"": ""{version}""");
        var packageJson = JsonSerializer.Deserialize(after, JsonSourceGenerationContext.Default.UpmPackageJson) ??
                          throw new ActionCommandException($"UPM package.json updated, but failed to load as valid JSON. contents: {after}");
        if (packageJson.Version != version)
            throw new ActionCommandException($"UPM package.json updated, but version miss-match. actual {packageJson.Version}, expected {version}");
        return after;
    }

    private static string UpdateGodot(string contents, string version)
    {
        var (_, after) = RegrexReplace.Replace(contents, @"(version=)""(.*?)""", $@"$1""{version}""");
        foreach (var line in after.Split('\n'))
        {
            if (!line.StartsWith("version=", StringComparison.Ordinal))
                continue;

            Span<Range> destination = stackalloc Range[2];
            var span = line.AsSpan();
            if (span.Split(destination, '=', StringSplitOptions.TrimEntries) != 2)
                continue;

            var versionValue = span[destination[1]].ToString();
            if (versionValue != $"\"{version}\"")
                throw new ActionCommandException($"Godot plugin.cfg updated, but version miss-match. actual {versionValue}, expected {version}");
            return after;
        }
        throw new ActionCommandException("Godot plugin.cfg updated, but version key not found.");
    }

    private static string UpdateDirectoryBuildProps(string contents, string version)
    {
        var (_, after) = RegrexReplace.Replace(contents, @"<VersionPrefix>.*</VersionPrefix>", $@"<VersionPrefix>{version}</VersionPrefix>");
        var xmlDoc = new System.Xml.XmlDocument();
        xmlDoc.LoadXml(after);
        var versionPrefixNode = xmlDoc.SelectSingleNode("//VersionPrefix") ??
                                throw new ActionCommandException("Directory.Build.props updated, but VersionPrefix key not found.");
        if (versionPrefixNode.InnerText != version)
            throw new ActionCommandException($"Directory.Build.props updated, but version miss-match. actual {versionPrefixNode.InnerText}, expected {version}");
        return after;
    }

    private readonly record struct PendingEdit(string Path, string Contents);
}
