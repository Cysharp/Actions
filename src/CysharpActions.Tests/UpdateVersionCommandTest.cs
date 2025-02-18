namespace CysharpActions.Tests;

public class UpdateVersionCommandTest
{
    [Fact]
    public void UpdateVersionsTest()
    {
        var version = "1.0.0";
        var dryRun = false;
        var baseDirectory = $".tests/{nameof(UpdateVersionCommandTest)}/{nameof(UpdateVersionsTest)}";

        // upm
        var unityPath = $"{baseDirectory}/upm/package.json";
        var unityFile = Path.GetFileName(unityPath);
        var unityContents = """
            {
              "name": "com.unity.plugin.example",
              "version": "1.2.310",
              "displayName": "Package Example Plugin",
              "description": "This is an example package",
              "unity": "2019.1",
              "unityRelease": "0b5",
              "dependencies": {
                "com.unity.example": "1.0.1"
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
            """.NormalizeEol();

        // godot
        var godotPath = $"{baseDirectory}/godot/plugin.cfg";
        var godotFile = Path.GetFileName(godotPath);
        var godotContent = """
            [plugin]

            name="Sandbox.Godot"
            description="Sample."
            author="Cysharp"
            version="1.2.310"
            language="C-sharp"
            script="GodotPlugin.cs"
            """.NormalizeEol();

        // Directory.Build.props
        var directoryBuildPropsPath = $"{baseDirectory}/dotnet/Directory.Build.props";
        var directoryBuildPropsFile = Path.GetFileName(directoryBuildPropsPath);
        var directoryBuildPropsContents = """
            <Project>
              <PropertyGroup>
                <VersionPrefix>1.2.310</VersionPrefix>
              </PropertyGroup>
            </Project>
            """.NormalizeEol();

        try
        {
            CreateFile(unityPath, unityContents);
            CreateFile(godotPath, godotContent);
            CreateFile(directoryBuildPropsPath, directoryBuildPropsContents);

            var command = new UpdateVersionCommand(version);
            var results = command.UpdateVersions([unityPath, godotPath, directoryBuildPropsPath], dryRun);
            foreach (var item in results)
            {
                if (item.Path == unityPath)
                {
                    Assert.Equal("""
                        {
                          "name": "com.unity.plugin.example",
                          "version": "1.0.0",
                          "displayName": "Package Example Plugin",
                          "description": "This is an example package",
                          "unity": "2019.1",
                          "unityRelease": "0b5",
                          "dependencies": {
                            "com.unity.example": "1.0.1"
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
                        """.NormalizeEol(), item.After);
                }
                else if (item.Path == godotPath)
                {
                    Assert.Equal("""
                        [plugin]

                        name="Sandbox.Godot"
                        description="Sample."
                        author="Cysharp"
                        version="1.0.0"
                        language="C-sharp"
                        script="GodotPlugin.cs"
                        """.NormalizeEol(), item.After);
                }
                else if (item.Path == directoryBuildPropsPath)
                {
                    Assert.Equal("""
                        <Project>
                          <PropertyGroup>
                            <VersionPrefix>1.0.0</VersionPrefix>
                          </PropertyGroup>
                        </Project>
                        """.NormalizeEol(), item.After);
                }
            }
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }
}