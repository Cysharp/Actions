namespace CysharpActions.Tests;

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

        Assert.Equal("Hello Japan!", after);
    }

    [Fact]
    public void CharacterChainReplaceTest()
    {
        var input = @"/a/b/c";
        var (_, after) = Utils.RegrexReplace.Replace(input, "a", "A");
        var (_, after2) = Utils.RegrexReplace.Replace(after, "b", "B");
        var (_, after3) = Utils.RegrexReplace.Replace(after2, "c", "C");

        Assert.Equal("/A/b/c", after);
        Assert.Equal("/A/B/c", after2);
        Assert.Equal("/A/B/C", after3);
    }

    [Fact]
    public void CharacterInsertTest()
    {
        var input = @"ABC DEF";
        var (_, after) = Utils.RegrexReplace.Replace(input, "ABC", "ABCD");

        Assert.Equal("ABCD DEF", after);
    }

    [Fact]
    public void CharacterDeleteTest()
    {
        var input = @"ABC DEF";
        var (_, after) = Utils.RegrexReplace.Replace(input, "ABC", "");

        Assert.Equal(" DEF", after);
    }

    [Fact]
    public void SentenceInsertTest()
    {
        var input = _dummyMultilineText;

        // insert sentence before the line contains `abcde`
        var (_, after) = Utils.RegrexReplace.Replace(input, $"^(.*?abcde.*?$)", "foo\n$1");

        Assert.Equal("""
            1 foobar
            foo
            2 abcde
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol(), after);
    }

    [Fact]
    public void SentenceDeleteTest()
    {
        var input = _dummyMultilineText;
        // delete the line contains `abcde`, but keep brank line as is
        var (_, after) = Utils.RegrexReplace.Replace(input, $"^(.*?abcde.*?$)", "");

        Assert.Equal("""
            1 foobar

            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol(), after);
    }

    [Fact]
    public void SentenceDeleteCompactionTest()
    {
        var input = _dummyMultilineText;

        // delete the line contains `abcde`, then compaction deleted line.
        var (_, after) = Utils.RegrexReplace.Replace(input, $"^(.*?abcde.*?$).*(\r?\n)?", "");

        Assert.Equal("""
            1 foobar
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol(), after);
    }

    [Fact]
    public void NoWriteBackTest()
    {
        var path = $"{nameof(NoWriteBackTest)}.txt";
        if (File.Exists(path)) File.Delete(path);

        File.WriteAllText(path, _dummyMultilineText);

        // delete the line contains `abcde`, then compaction deleted line.
        var (before, after) = Utils.RegrexReplace.Replace(path, $"^(.*?abcde.*?$).*(\r?\n)?", "", false);

        Assert.Equal("""
            1 foobar
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol(), after);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void WriteBackTest()
    {
        var path = $"{nameof(WriteBackTest)}.txt";
        if (File.Exists(path)) File.Delete(path);

        File.WriteAllText(path, _dummyMultilineText);

        // delete the line contains `abcde`, then compaction deleted line.
        var (_, after) = Utils.RegrexReplace.Replace(path, $"^(.*?abcde.*?$).*(\r?\n)?", "", true);

        Assert.Equal("""
            1 foobar
            3 piyopiyo
            4 okonomiyaki
            5 takoyaki
            """.NormalizeEol(), after);
        Assert.Equal(after, File.ReadAllText(path));
    }
}
