using Actions.Commands;
using FluentAssertions;

namespace Actions.Tests
{
    public class VersioningCommandTest
    {
        [Theory]
        [InlineData("0.1.0", VersionIncrement.Major, "1.1.0")]
        [InlineData("0.1.0", VersionIncrement.Minor, "0.2.0")]
        [InlineData("0.1.0", VersionIncrement.Patch, "0.1.1")]
        public void VersionIncrementTest(string tag, VersionIncrement versionIncrement, string actual)
        {
            var command = new VersioningCommand(tag, prefix: "", versionIncrement: versionIncrement, isPrelease: false, prerelease: "");
            var versioning = command.Versioning();

            versioning.Should().Be(actual);
        }

        [Theory]
        [InlineData("v0.1.0", "v", true, "0.1.1")]
        [InlineData("Ver0.1.0", "Ver", true, "0.1.1")]
        [InlineData("Ver.0.1.0", "Ver.", true, "0.1.1")]
        [InlineData("v0.1.0", "v", false, "v0.1.1")]
        [InlineData("Ver0.1.0", "Ver", false, "Ver0.1.1")]
        [InlineData("Ver.0.1.0", "Ver.", false, "Ver.0.1.1")]
        public void VersionPrefixTest(string tag, string prefix, bool withoutPrefix, string actual)
        {
            var command = new VersioningCommand(tag, prefix: prefix, versionIncrement: VersionIncrement.Patch, isPrelease: false, prerelease: "");
            var versioning = command.Versioning(withoutPrefix);

            versioning.Should().Be(actual);
        }

        [Theory]
        [InlineData("0.1.0", "", "0.1.1")]
        [InlineData("0.1.0", "alpha", "0.1.1-alpha")]
        [InlineData("0.1.0", "preview", "0.1.1-preview")]
        public void VersionPrereleaseTest(string tag, string prerelease, string actual)
        {
            var command = new VersioningCommand(tag, prefix: "", versionIncrement: VersionIncrement.Patch, isPrelease: true, prerelease: prerelease);
            var versioning = command.Versioning();

            versioning.Should().Be(actual);
        }
    }
}