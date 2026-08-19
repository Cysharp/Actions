using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        var utf8 = Encoding.UTF8.GetBytes(contents);
        var reader = new Utf8JsonReader(utf8);
        var replacementStart = -1;
        var valueEnd = -1;
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName ||
                reader.CurrentDepth != 1 ||
                !reader.ValueTextEquals("version"))
            {
                continue;
            }

            replacementStart = checked((int)reader.TokenStartIndex);
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                throw new ActionCommandException("UPM package.json version must be a string.");

            valueEnd = checked((int)reader.BytesConsumed);
            break;
        }

        if (replacementStart < 0)
            throw new ActionCommandException("UPM package.json version key not found.");

        var replacement = Encoding.UTF8.GetBytes($"\"version\": {JsonSerializer.Serialize(version)}");
        var updatedUtf8 = new byte[utf8.Length - (valueEnd - replacementStart) + replacement.Length];
        utf8.AsSpan(0, replacementStart).CopyTo(updatedUtf8);
        replacement.CopyTo(updatedUtf8.AsSpan(replacementStart));
        utf8.AsSpan(valueEnd).CopyTo(updatedUtf8.AsSpan(replacementStart + replacement.Length));
        var after = Encoding.UTF8.GetString(updatedUtf8);

        var packageJson = JsonSerializer.Deserialize(after, JsonSourceGenerationContext.Default.UpmPackageJson) ??
                          throw new ActionCommandException($"UPM package.json updated, but failed to load as valid JSON. contents: {after}");
        if (packageJson.Version != version)
            throw new ActionCommandException($"UPM package.json updated, but version miss-match. actual {packageJson.Version}, expected {version}");
        return after;
    }

    private static string UpdateGodot(string contents, string version)
    {
        if (version.IndexOfAny(['"', '\r', '\n']) >= 0)
            throw new ActionCommandException("Godot plugin.cfg version contains an invalid character.");

        var sectionHeader = Regex.Match(contents, @"^[ \t]*\[plugin\][ \t]*\r?$", RegexOptions.Multiline);
        if (!sectionHeader.Success)
            throw new ActionCommandException("Godot plugin.cfg [plugin] section not found.");

        var sectionStart = sectionHeader.Index + sectionHeader.Length;
        var nextSection = Regex.Match(
            contents,
            @"^[ \t]*\[[^\]\r\n]+\][ \t]*\r?$",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(1));
        while (nextSection.Success && nextSection.Index <= sectionHeader.Index)
            nextSection = nextSection.NextMatch();

        var sectionEnd = nextSection.Success ? nextSection.Index : contents.Length;
        var section = contents[sectionStart..sectionEnd];
        var versionMatch = Regex.Match(
            section,
            @"^(?<prefix>[ \t]*version[ \t]*=[ \t]*)""(?<value>[^""\r\n]*)""",
            RegexOptions.Multiline);
        if (!versionMatch.Success)
            throw new ActionCommandException("Godot plugin.cfg version key not found in [plugin] section.");

        var value = versionMatch.Groups["value"];
        var valueStart = sectionStart + value.Index;
        return contents[..valueStart] + version + contents[(valueStart + value.Length)..];
    }

    private static string UpdateDirectoryBuildProps(string contents, string version)
    {
        var after = Regex.Replace(
            contents,
            @"(?<=<VersionPrefix>)[^<]*(?=</VersionPrefix>)",
            _ => version);
        var xmlDoc = new System.Xml.XmlDocument();
        xmlDoc.LoadXml(after);
        var versionPrefixNodes = xmlDoc.SelectNodes("//VersionPrefix");
        if (versionPrefixNodes == null || versionPrefixNodes.Count == 0)
            throw new ActionCommandException("Directory.Build.props updated, but VersionPrefix key not found.");
        foreach (System.Xml.XmlNode versionPrefixNode in versionPrefixNodes)
        {
            if (versionPrefixNode.InnerText != version)
                throw new ActionCommandException($"Directory.Build.props updated, but version miss-match. actual {versionPrefixNode.InnerText}, expected {version}");
        }
        return after;
    }

    private readonly record struct PendingEdit(string Path, string Contents);
}
