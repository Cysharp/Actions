using FluentAssertions;

namespace Actions.Tests;

public class RegexReplacerTest
{
    private readonly string _dummyMultilineText;

    public RegexReplacerTest()
    {
        _dummyMultilineText = """
            1 foobar
            2 abcde
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol();
    }

    [Fact]
    public void CharacterReplaceTest()
    {
        var input = @"Hello world!";
        var (_, after) = Utils.RegrexReplace.Replace(input, "world", "Japan");

        after.Should().Be("Hello Japan!");
    }

    [Fact]
    public void CharacterChainReplaceTest()
    {
        var input = @"/a/b/c";
        var (_, after) = Utils.RegrexReplace.Replace(input, "a", "A");
        var (_, after2) = Utils.RegrexReplace.Replace(after, "b", "B");
        var (_, after3) = Utils.RegrexReplace.Replace(after2, "c", "C");

        after.Should().Be("/A/b/c");
        after2.Should().Be("/A/B/c");
        after3.Should().Be("/A/B/C");
    }

    [Fact]
    public void CharacterInsertTest()
    {
        var input = @"ABC DEF";
        var (_, after) = Utils.RegrexReplace.Replace(input, "ABC", "ABCD");

        after.Should().Be("ABCD DEF");
    }

    [Fact]
    public void CharacterDeleteTest()
    {
        var input = @"ABC DEF";
        var (_, after) = Utils.RegrexReplace.Replace(input, "ABC", "");

        after.Should().Be(" DEF");
    }

    [Fact]
    public void SentenceInsertTest()
    {
        var input = _dummyMultilineText;

        // insert sentence before the line contains `abcde`
        var (_, after) = Utils.RegrexReplace.Replace(input, $"^(.*?abcde.*?$)", "foo\n$1");

        after.Should().Be("""
            1 foobar
            foo
            2 abcde
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol());
    }

    [Fact]
    public void SentenceDeleteTest()
    {
        var input = _dummyMultilineText;
        // delete the line contains `abcde`, but keep brank line as is
        var (_, after) = Utils.RegrexReplace.Replace(input, $"^(.*?abcde.*?$)", "");

        after.Should().Be("""
            1 foobar

            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol());
    }

    [Fact]
    public void SentenceDeleteCompactionTest()
    {
        var input = _dummyMultilineText;

        // delete the line contains `abcde`, then compaction deleted line.
        var (_, after) = Utils.RegrexReplace.Replace(input, $"^(.*?abcde.*?$).*(\r?\n)?", "");

        after.Should().Be("""
            1 foobar
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol());
    }

    [Fact]
    public void NoWriteBackTest()
    {
        var path = $"{nameof(NoWriteBackTest)}.txt";
        if (File.Exists(path)) File.Delete(path);

        File.WriteAllText(path, _dummyMultilineText);

        // delete the line contains `abcde`, then compaction deleted line.
        var (before, after) = Utils.RegrexReplace.Replace(path, $"^(.*?abcde.*?$).*(\r?\n)?", "", false);

        after.Should().Be("""
            1 foobar
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol());
        File.ReadAllText(path).Should().Be(before);
    }

    [Fact]
    public void WriteBackTest()
    {
        var path = $"{nameof(WriteBackTest)}.txt";
        if (File.Exists(path)) File.Delete(path);

        File.WriteAllText(path, _dummyMultilineText);

        // delete the line contains `abcde`, then compaction deleted line.
        var (_, after) = Utils.RegrexReplace.Replace(path, $"^(.*?abcde.*?$).*(\r?\n)?", "", true);

        after.Should().Be("""
            1 foobar
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol());
        File.ReadAllText(path).Should().Be(after);
    }
}

public static class StringExtentions
{
    public static string NormalizeEol(this string input)
    {
        return input.Replace("\r\n", "\n");
    }
}
