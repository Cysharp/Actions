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
    [InlineData("0.1.0", false)]
    [InlineData("1.0.0", false)]
    [InlineData("1.1.0", false)]
    [InlineData("1.2.0", true)] // Current Release Tag is 1.2.0
    [InlineData("1.2.1", true)]
    [InlineData("999.0.0", true)]
    public async Task ValidateTest(string tag, bool expected)
    {
        var command = new ValidateTagCommand();
        var (success, releaseTag) = await command.ValidateTagAsync(tag);

        success.Should().Be(expected);
    }
}