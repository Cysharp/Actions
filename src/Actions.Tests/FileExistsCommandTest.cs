using Actions.Commands;

namespace Actions.Tests;

public class FileExistsCommandTest
{
    [Fact]
    public void SkipEmptyPathTest()
    {
        var command = new FileExsistsCommand("");
        command.Validate();
    }

    [Fact]
    public void FullPathTest()
    {
        var path = $".tests/{nameof(FileExistsCommandTest)}/{nameof(FullPathTest)}/dummy.nupkg";
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, "");

        var command = new FileExsistsCommand(path);
        command.Validate();
    }

    [Fact]
    public void WildcardFileTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardFileTest)}";
        var items = new[] { "foo", "bar", "piyo", "test.txt", "hoge.txt" };
        foreach (var item in items)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, item), "");
        }

        var command = new FileExsistsCommand($"{dir}/*");
        command.Validate();

        var command2 = new FileExsistsCommand($"{dir}/foo");
        command2.Validate();

        var command3 = new FileExsistsCommand($"{dir}/*.txt");
        command3.Validate();

        var command4 = new FileExsistsCommand($"{dir}/hoge.*");
        command4.Validate();
    }

    [Fact]
    public void WildcardDirectoryTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardDirectoryTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        foreach (var item in items)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, item), "");
        }

        var command = new FileExsistsCommand($".tests/{nameof(FileExistsCommandTest)}/*/foo");
        command.Validate();

        var command2 = new FileExsistsCommand($".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardDirectoryTest)}/foo");
        command2.Validate();

        var failCommand = new FileExsistsCommand($".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardDirectoryTest)}/*/foo");
        Assert.Throws<ActionCommandException>(() => failCommand.Validate());
    }

    [Fact]
    public void RecursiveWildcardDirectoryTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(RecursiveWildcardDirectoryTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        foreach (var item in items)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, item), "");
        }

        var command = new FileExsistsCommand($".tests/{nameof(FileExistsCommandTest)}/**/foo");
        command.Validate();

        var command2 = new FileExsistsCommand($".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardDirectoryTest)}/**/foo");
        command2.Validate();
    }

    [Fact]
    public void RecursiveWildcardDirectoryAndFileTest()
    {
        var dirBase = $".tests/{nameof(FileExistsCommandTest)}/{nameof(RecursiveWildcardDirectoryAndFileTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        foreach (var item in items)
        {
            var dir = Path.Combine(dirBase, item);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, item), "");
        }

        var command = new FileExsistsCommand($"{dirBase}/**/*");
        command.Validate();

        var command2 = new FileExsistsCommand($"{dirBase}/foo/foo");
        command2.Validate();
    }
}