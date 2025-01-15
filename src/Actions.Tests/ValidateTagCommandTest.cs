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
    [InlineData("1.2.0")]// Current Release Tag is 1.2.0
    [InlineData("999.0.0")]
    public async Task ValidateSuccessTest(string tag)
    {
        var command = new ValidateTagCommand();
        await command.ValidateTagAsync(tag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0.1.0")]
    [InlineData("1.0.0")]
    public async Task ValidateFailTest(string tag)
    {
        var command = new ValidateTagCommand();
        await Assert.ThrowsAsync<ActionCommandException>(() => command.ValidateTagAsync(tag));
    }
}