namespace CysharpActions.Tests;

public class VersioningCommandTest
{
    [Theory]
    [InlineData("1.0.0", VersionIncrement.Major, "2.0.0")]
    [InlineData("1.0.0", VersionIncrement.Minor, "1.1.0")]
    [InlineData("1.0.0", VersionIncrement.Patch, "1.0.1")]
    public void VersionIncrementTest(string tag, VersionIncrement versionIncrement, string expected)
    {
        var command = new VersioningCommand();
        var actual = command.UpdateVersion(tag, versionIncrement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("v1.0.0", VersionIncrement.Patch, "v", "v1.0.1")]
    [InlineData("Ver1.0.0", VersionIncrement.Patch, "Ver", "Ver1.0.1")]
    [InlineData("Ver.1.0.0", VersionIncrement.Patch, "Ver.", "Ver.1.0.1")]
    public void VersionPrefixTest(string tag, VersionIncrement versionIncrement, string prefix, string expected)
    {
        var command = new VersioningCommand(prefix: prefix);
        var actual = command.UpdateVersion(tag, versionIncrement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1.0.0-alpha.1", VersionIncrement.Patch, "-alpha.1", "1.0.1-alpha.1")]
    [InlineData("1.0.0-preview.7", VersionIncrement.Patch, "-preview.7", "1.0.1-preview.7")]
    public void VersionSuffixTest(string tag, VersionIncrement versionIncrement, string suffix, string expected)
    {
        var command = new VersioningCommand(suffix: suffix);
        var actual = command.UpdateVersion(tag, versionIncrement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("v1.0.0-alpha.1", VersionIncrement.Patch, "v", "-alpha.1", "v1.0.1-alpha.1")]
    [InlineData("Ver.1.0.0-preview.7", VersionIncrement.Patch, "Ver.", "-preview.7", "Ver.1.0.1-preview.7")]
    public void VersionPrefixSuffixTest(string tag, VersionIncrement versionIncrement, string prefix, string suffix, string expected)
    {
        var command = new VersioningCommand(prefix, suffix);
        var actual = command.UpdateVersion(tag, versionIncrement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1.0.0", VersionIncrement.Patch, "v", "", "v1.0.1")]
    [InlineData("1.0.0-preview.7", VersionIncrement.Patch, "Ver.", "-preview.7", "Ver.1.0.1-preview.7")]
    public void VersionAddPrefixTest(string tag, VersionIncrement versionIncrement, string prefix, string suffix, string expected)
    {
        var command = new VersioningCommand(prefix, suffix);
        var actual = command.UpdateVersion(tag, versionIncrement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1.0.0", VersionIncrement.Patch, "", "-alpha.1", "1.0.1-alpha.1")]
    [InlineData("Ver.1.0.0", VersionIncrement.Patch, "Ver.", "-preview.7", "Ver.1.0.1-preview.7")]
    public void VersionAddSuffixTest(string tag, VersionIncrement versionIncrement, string prefix, string suffix, string expected)
    {
        var command = new VersioningCommand(prefix, suffix);
        var actual = command.UpdateVersion(tag, versionIncrement);

        Assert.Equal(expected, actual);
    }
}