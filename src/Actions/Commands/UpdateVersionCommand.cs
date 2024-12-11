using Actions.Utils;

namespace Actions.Commands
{
    public class UpdateVersionCommand(string version, string path)
    {
        public (string before, string after) UpdateVersion(bool dryRun)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            var fileName = Path.GetFileName(path);
            return fileName switch
            {
                // UPM
                "package.json" => Sed.Replace(path, @"""version"":\s*""(.*?)""", $@"""version"": ""{version}""", dryRun),
                // Godot
                "plugin.cfg" => Sed.Replace(path, @"(version=)""(.*?)""", $@"$1""{version}""", dryRun),
                // .NET
                "Directory.Build.props" => Sed.Replace(path, @"<VersionPrefix>.*</VersionPrefix>", $@"<VersionPrefix>{version}</VersionPrefix>", dryRun),
                _ => throw new NotImplementedException(fileName),
            };
        }
    }
}
