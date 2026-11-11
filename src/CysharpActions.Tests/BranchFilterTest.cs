using CysharpActions.Utils;

namespace CysharpActions.Tests;

public class BranchFilterTest
{
    [Theory]
    // * does not match /, ** does.
    [InlineData("release/*", "release/6.x", true)]
    [InlineData("release/*", "release/", true)]
    [InlineData("release/*", "release/6.x/foo", false)]
    [InlineData("release/*", "release", false)]
    [InlineData("release/**", "release/6.x/foo", true)]
    [InlineData("*", "main", true)]
    [InlineData("*", "feature/x", false)]
    [InlineData("**", "feature/x", true)]
    [InlineData("***", "feature/x", true)]
    // Literal names match whole names only, case-sensitively, and . is not a wildcard.
    [InlineData("v4", "v4", true)]
    [InlineData("v4", "v4_test", false)]
    [InlineData("v4", "xv4", false)]
    [InlineData("v4", "V4", false)]
    [InlineData("v4.x", "v4.x", true)]
    [InlineData("v4.x", "v4ax", false)]
    // Digits-only pattern for MessagePack-style major version branches.
    [InlineData("v[0-9]+", "v2", true)]
    [InlineData("v[0-9]+", "v10", true)]
    [InlineData("v[0-9]+", "v", false)]
    [InlineData("v[0-9]+", "v4_test", false)]
    [InlineData("v[0-9]+", "v4-test", false)]
    [InlineData("v[234]", "v3", true)]
    [InlineData("v[234]", "v5", false)]
    [InlineData("v[a-zA-Z0-9]", "vQ", true)]
    // ? and + repeat the previous character.
    [InlineData("colou?r", "color", true)]
    [InlineData("colou?r", "colour", true)]
    [InlineData("colou?r", "colouur", false)]
    [InlineData("ab+c", "abbbc", true)]
    [InlineData("ab+c", "ac", false)]
    // \ escapes special characters, so \+ and \* match themselves.
    [InlineData(@"a\+b", "a+b", true)]
    [InlineData(@"a\+b", "aab", false)]
    [InlineData(@"\!x", "!x", true)]
    // ! is special only at the start.
    [InlineData("a!b", "a!b", true)]
    public void SinglePatternTest(string pattern, string branch, bool expected)
    {
        Assert.Equal(expected, BranchFilter.Parse([pattern]).IsMatch(branch));
    }

    [Theory]
    [InlineData("release/6.x", true)]
    [InlineData("release/5.x", false)]
    [InlineData("release/5.x-hotfix", true)]
    [InlineData("feature/x", false)]
    public void LaterPatternsOverrideEarlierPatternsTest(string branch, bool expected)
    {
        // A leading ! excludes names that earlier patterns matched, and a later positive pattern includes them again.
        var filter = BranchFilter.Parse(["release/*", "!release/5.*", "release/5.x-hotfix"]);
        Assert.Equal(expected, filter.IsMatch(branch));
    }

    [Fact]
    public void NegationAloneMatchesNothingTest()
    {
        var filter = BranchFilter.Parse(["!release/*"]);
        Assert.False(filter.IsMatch("main"));
        Assert.False(filter.IsMatch("release/6.x"));
    }

    [Fact]
    public void EmptyFilterMatchesNothingTest()
    {
        var filter = BranchFilter.Parse([]);
        Assert.False(filter.IsMatch("main"));
        Assert.Empty(filter.Patterns);
    }

    [Theory]
    [InlineData("!")]
    [InlineData("?v")]
    [InlineData("+v")]
    [InlineData("*+")]
    [InlineData("**?")]
    [InlineData("v++")]
    [InlineData("v+?")]
    [InlineData("v[0-9")]
    [InlineData("v[]")]
    [InlineData("v[^0-9]")]
    [InlineData("v[!0-9]")]
    [InlineData("v[0-]")]
    [InlineData("v[-9]")]
    [InlineData("v[9-0]")]
    [InlineData("v[a-Z]")]
    [InlineData("v[0-z]")]
    [InlineData("v[.]")]
    [InlineData(@"v\")]
    public void InvalidPatternIsRejectedTest(string pattern)
    {
        var exception = Assert.Throws<ActionCommandException>(() => BranchFilter.Parse(["main", pattern]));
        Assert.Contains($"'{pattern}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PatternsKeepOrderTest()
    {
        Assert.Equal(["release/*", "!release/5.x"], BranchFilter.Parse(["release/*", "!release/5.x"]).Patterns);
    }
}
