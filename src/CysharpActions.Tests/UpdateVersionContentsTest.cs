namespace CysharpActions.Tests;

public class UpdateVersionContentsTest
{
    [Theory]
    [InlineData("package.json", "{\"name\":\"example\",\"version\":\"1.0.0\"}", "\"version\": \"2.0.0\"")]
    [InlineData("plugin.cfg", "[plugin]\nversion=\"1.0.0\"\n", "version=\"2.0.0\"")]
    [InlineData("Directory.Build.props", "<Project><PropertyGroup><VersionPrefix>1.0.0</VersionPrefix></PropertyGroup></Project>", "<VersionPrefix>2.0.0</VersionPrefix>")]
    public void UpdateContentsTest(string fileName, string contents, string expected)
    {
        var updated = UpdateVersionCommand.UpdateContents(fileName, contents, "2.0.0");

        Assert.Contains(expected, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedFileTest()
    {
        var exception = Assert.Throws<ActionCommandException>(() =>
            UpdateVersionCommand.UpdateContents("version.txt", "1.0.0", "2.0.0"));

        Assert.Contains("version.txt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateUpmChangesOnlyRootVersionTest()
    {
        const string contents = """
            {
              "version": "1.0.0",
              "metadata": {
                "version": "schema-1"
              }
            }
            """;

        var updated = UpdateVersionCommand.UpdateContents("package.json", contents, "2.0.0");

        Assert.Equal("""
            {
              "version": "2.0.0",
              "metadata": {
                "version": "schema-1"
              }
            }
            """, updated);
    }

    [Fact]
    public void UpdateGodotChangesOnlyPluginVersionTest()
    {
        const string contents = """
            [application]
            version="application-1"

            [plugin]
            minimum_version="4.0"
            version="1.0.0"

            [other]
            version="other-1"
            """;

        var updated = UpdateVersionCommand.UpdateContents("plugin.cfg", contents, "2.0.0");

        Assert.Equal("""
            [application]
            version="application-1"

            [plugin]
            minimum_version="4.0"
            version="2.0.0"

            [other]
            version="other-1"
            """, updated);
    }

    [Fact]
    public void UpdateDirectoryBuildPropsPreservesMultipleVersionPrefixElementsTest()
    {
        const string contents = "<Project><PropertyGroup><VersionPrefix>1.0.0</VersionPrefix><Other>keep</Other><VersionPrefix>1.1.0</VersionPrefix></PropertyGroup></Project>";

        var updated = UpdateVersionCommand.UpdateContents("Directory.Build.props", contents, "2.0.0");

        Assert.Equal("<Project><PropertyGroup><VersionPrefix>2.0.0</VersionPrefix><Other>keep</Other><VersionPrefix>2.0.0</VersionPrefix></PropertyGroup></Project>", updated);
    }
}
