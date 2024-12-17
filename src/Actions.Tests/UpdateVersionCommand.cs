using Actions.Commands;
using FluentAssertions;

namespace Actions.Tests;

public class UpdateVersionCommandTest
{
    [Fact]
    public void UpdateVersionUpmTest()
    {
        var version = "1.0.0";
        var path = $"{nameof(UpdateVersionCommandTest)}/package.json";
        var contents = """
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
            """;

        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, contents);

        var command = new UpdateVersionCommand(version, path);
        var (before, after) = command.UpdateVersion(false);

        after.Should().Be("""
            {
              "name": "com.unity.plugin.example",
              "version": "1.0.0",
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
    }

    [Fact]
    public void UpdateVersionGodotTest()
    {
        var version = "1.0.0";
        var path = $"{nameof(UpdateVersionGodotTest)}/plugin.cfg";
        var contents = """
            [plugin]

            name="Sandbox.Godot"
            description="Sample."
            author="Cysharp"
            version="1.2.310"
            language="C-sharp"
            script="GodotPlugin.cs"
            """;

        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, contents);

        var command = new UpdateVersionCommand(version, path);
        var (before, after) = command.UpdateVersion(false);

        after.Should().Be("""
            [plugin]
            
            name="Sandbox.Godot"
            description="Sample."
            author="Cysharp"
            version="1.0.0"
            language="C-sharp"
            script="GodotPlugin.cs"
            """);
    }

    [Fact]
    public void UpdateVersionDirectoryBuildPropsTest()
    {
        var version = "1.0.0";
        var path = $"{nameof(UpdateVersionDirectoryBuildPropsTest)}/Directory.Build.props";
        var contents = """
            <Project>
              <PropertyGroup>
                <VersionPrefix>1.2.310</VersionPrefix>
              </PropertyGroup>
            </Project>
            """;

        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, contents);

        var command = new UpdateVersionCommand(version, path);
        var (before, after) = command.UpdateVersion(false);

        after.Should().Be("""
            <Project>
              <PropertyGroup>
                <VersionPrefix>1.0.0</VersionPrefix>
              </PropertyGroup>
            </Project>
            """);
    }
}