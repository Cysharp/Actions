namespace CysharpActions.Tests;

public class VersioningCommandTest
{
    [Theory]
    [InlineData("0.1.0", VersionIncrement.Major, "1.1.0")]
    [InlineData("0.1.0", VersionIncrement.Minor, "0.2.0")]
    [InlineData("0.1.0", VersionIncrement.Patch, "0.1.1")]
    public void VersionIncrementTest(string tag, VersionIncrement versionIncrement, string expected)
    {
        var command = new VersioningCommand(tag, prefix: "", versionIncrement: versionIncrement, isPrelease: false, prerelease: "");
        var actual = command.Versioning();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("v0.1.0", "v", true, "0.1.1")]
    [InlineData("Ver0.1.0", "Ver", true, "0.1.1")]
    [InlineData("Ver.0.1.0", "Ver.", true, "0.1.1")]
    [InlineData("v0.1.0", "v", false, "v0.1.1")]
    [InlineData("Ver0.1.0", "Ver", false, "Ver0.1.1")]
    [InlineData("Ver.0.1.0", "Ver.", false, "Ver.0.1.1")]
    public void VersionPrefixTest(string tag, string prefix, bool withoutPrefix, string expected)
    {
        var command = new VersioningCommand(tag, prefix: prefix, versionIncrement: VersionIncrement.Patch, isPrelease: false, prerelease: "");
        var actual = command.Versioning(withoutPrefix);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("0.1.0", "", "0.1.1")]
    [InlineData("0.1.0", "alpha", "0.1.1-alpha")]
    [InlineData("0.1.0", "preview", "0.1.1-preview")]
    public void VersionPrereleaseTest(string tag, string prerelease, string expected)
    {
        var command = new VersioningCommand(tag, prefix: "", versionIncrement: VersionIncrement.Patch, isPrelease: true, prerelease: prerelease);
        var actual = command.Versioning();

        Assert.Equal(expected, actual);
    }
}