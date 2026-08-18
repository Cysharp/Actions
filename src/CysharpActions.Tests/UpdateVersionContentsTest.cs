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
}
