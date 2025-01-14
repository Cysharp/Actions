using Actions.Commands;
using FluentAssertions;

namespace Actions.Tests;

public class ValidateTagCommandTest
{
    [Theory]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("v0.1.0", "0.1.0")]
    [InlineData("v10.1.0", "10.1.0")]
    public void NormalizeTest(string tag, string expected)
    {
        var command = new ValidateTagCommand();
        var actual = command.Normalize(tag);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("", ValidateTagResult.InvalidMissingTag, 1, false)]
    [InlineData("0.1.0", ValidateTagResult.InvalidReverting, 1, false)]
    [InlineData("1.0.0", ValidateTagResult.InvalidReverting, 1, false)]
    [InlineData("1.2.0", ValidateTagResult.ValidVersionSame, 0, true)]// Current Release Tag is 1.2.0
    [InlineData("999.0.0", ValidateTagResult.ValidVersionNewer, 0, true)]
    public async Task ValidateTest(string tag, ValidateTagResult expectedResult, int expectedExitCode, bool expected)
    {
        var command = new ValidateTagCommand();
        var (validated, reason, releaseTag) = await command.ValidateTagAsync(tag);

        reason.Should().Be(expectedResult);
        reason.ToExitCode().Should().Be(expectedExitCode);
        validated.Should().Be(expected);
    }
}