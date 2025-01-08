using Actions.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Actions.Commands;

public record struct UpdateVersionCommandResult(string Before, string After);
public class UpdateVersionCommand(string version)
{
    public UpdateVersionCommandResult UpdateVersion(string path, bool dryRun)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);

        var writeBack = !dryRun;
        var fileName = Path.GetFileName(path);
        return fileName switch
        {
            // UPM
            "package.json" => HandleUpm(path, writeBack),
            // Godot
            "plugin.cfg" => HandleGodot(path, writeBack),
            // .NET
            "Directory.Build.props" => HandleDirectoryBuildProps(path, writeBack),
            // Other
            _ => throw new NotImplementedException(fileName),
        };
    }

    private UpdateVersionCommandResult HandleUpm(string path, bool writeBack)
    {
        // replace
        var (before, after) = Sed.Replace(path, @"""version"":\s*""(.*?)""", $@"""version"": ""{version}""", writeBack);

        // validate
        Validate(after, version);

        return new UpdateVersionCommandResult(before, after);

        static void Validate(string contents, string version)
        {
            var packageJson = JsonSerializer.Deserialize<UpmPackageJson>(contents) ?? throw new ActionCommandException($"UPM package.json updated, but failed to load as valid JSON. contents: {contents}");
            if (packageJson.Version != version)
                throw new ActionCommandException($"UPM package.json updated, but version miss-match. actual {packageJson?.Version}, expected {version}");
        }
    }

    private UpdateVersionCommandResult HandleGodot(string path, bool writeBack)
    {
        // replace
        var (before, after) = Sed.Replace(path, @"(version=)""(.*?)""", $@"$1""{version}""", writeBack);

        // validate
        Validate(after, version);

        return new UpdateVersionCommandResult(before, after);

        static void Validate(string contents, string version)
        {
            var lines = contents.Split("\n");
            Span<Range> destination = stackalloc Range[2];
            foreach (var line in lines)
            {
                // find the line befin with "version=", then split with = to get version
                if (!line.StartsWith("version="))
                    continue;

                var span = line.AsSpan();
                var range = span.Split(destination, '=', StringSplitOptions.TrimEntries);
                if (range != 2)
                    continue;

                // validate version is expceted
                var versionValue = span[destination[1]].ToString();
                if (versionValue != $"\"{version}\"")
                {
                    throw new ActionCommandException($"Godot plugin.cfg updated, but version miss-match. actual {versionValue}, expected {version}");
                }
                return;
            }
            throw new ActionCommandException($"Godot plugin.cfg updated, but version key not found.");
        }
    }

    private UpdateVersionCommandResult HandleDirectoryBuildProps(string path, bool writeBack)
    {
        // replace
        var (before, after) = Sed.Replace(path, @"<VersionPrefix>.*</VersionPrefix>", $@"<VersionPrefix>{version}</VersionPrefix>", writeBack);

        // validate
        Validate(after, version);

        return new UpdateVersionCommandResult(before, after);

        static void Validate(string contents, string version)
        {
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(contents);
            var versionPrefixNode = xmlDoc.SelectSingleNode("//VersionPrefix") ?? throw new ActionCommandException($"Directory.Build.props updated, but VersionPrefix key not found.");
            if (versionPrefixNode.InnerText != version)
                throw new ActionCommandException($"Directory.Build.props updated, but version miss-match. actual {versionPrefixNode.InnerText}, expected {version}");

        }
    }

    private record UpmPackageJson
    {
        [JsonPropertyName("version")]
        public required string Version { get; set; }
    }
}
