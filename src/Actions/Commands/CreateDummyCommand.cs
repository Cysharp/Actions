namespace Actions.Commands
{
    public class CreateDummyCommand
    {
        public void CreateDummyFiles(string basePath)
        {
            var upm = ("package.json", """
                {
                    "name": "com.unity.plugin.example",
                    "version": "1.2.310",
                    "displayName": "Package Example Plugin",
                    "description": "This is an example package",
                    "unity": "2019.1",
                    "unityRelease": "0b5",
                    "dependencies": {
                        "com.unity.example": "1.0.0"
                    },
                    "keywords": [
                        "keyword1",
                        "keyword2",
                        "keyword3"
                    ],
                    "author": {
                    "name": "Unity",
                    "email": "unity@example.com",
                    "url": "https://www.unity3d.com"
                    }
                }
                """);
            var godot = ("plugin.cfg", """
                [plugin]
                name="Sandbox.Godot"
                description="Sample."
                author="Cysharp"
                version="1.2.310"
                language="C-sharp"
                script="GodotPlugin.cs"
                """);
            var directoryBuildProps = ("Directory.Build.props", """
                <Project>
                  <PropertyGroup>
                    <VersionPrefix>1.2.310</VersionPrefix>
                  </PropertyGroup>
                </Project>                
                """);

            foreach (var (file, contents) in new[] { upm, godot, directoryBuildProps })
            {
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                var path = Path.Combine(basePath, file);
                File.WriteAllText(path, contents);
            }
        }
    }
}
