namespace CysharpActions.Tests;

public class FileExistsCommandTest
{
    [Fact]
    public void SkipEmptyPathTest()
    {
        var command = new FileExsistsCommand();
        command.Validate("");
    }

    [Fact]
    public void AllowMissingTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(FullPathTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        foreach (var item in items)
        {
            // file not exists, but test should pass
            var command = new FileExsistsCommand(allowMissing: true);
            command.Validate(Path.Combine(dir, item));
            command.Validate(Path.Combine(dir, "**", item));
        }
    }

    [Fact]
    public void FullPathTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(FullPathTest)}";
        var item = "dummy.nupkg";
        try
        {
            CreateFiles(dir, [item], false);
            var command = new FileExsistsCommand();
            command.Validate(Path.Combine(dir, item));
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public void WildcardFileTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardFileTest)}";
        var items = new[] { "foo", "bar", "piyo", "test.txt", "hoge.txt" };
        try
        {
            CreateFiles(dir, items, false);
            var command = new FileExsistsCommand();
            foreach (var item in items)
            {
                command.Validate($"{dir}/{item}");
            }
            command.Validate($"{dir}/*");
            command.Validate($"{dir}/*.txt");
            command.Validate($"{dir}/hoge.*");
            command.Validate($"{dir}/**/hoge.*");
            command.Validate($"{dir}/**/*");
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public void WildcardDirectoryTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(WildcardDirectoryTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        try
        {
            CreateFiles(dir, items, false);
            var command = new FileExsistsCommand();
            foreach (var item in items)
            {
                command.Validate($"{Path.GetDirectoryName(dir)}/*/{item}");
                command.Validate($"{dir}/{item}");
                command.Validate($"{dir}/**/{item}");
                command.Validate($"{dir}/**/*");
                Assert.Throws<ActionCommandException>(() => command.Validate($"{dir}/*/{item}"));
            }
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public void RecursiveWildcardDirectoryTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(RecursiveWildcardDirectoryTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        try
        {
            CreateFiles(dir, items, false);
            var command = new FileExsistsCommand();
            foreach (var item in items)
            {
                command.Validate($"{Path.GetDirectoryName(dir)}/**/{item}");
                command.Validate($"{dir}/**/{item}");
            }
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public void RecursiveWildcardDirectoryAndFileTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(RecursiveWildcardDirectoryAndFileTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        try
        {
            CreateFiles(dir, items, true);
            var command = new FileExsistsCommand();            
            command.Validate($"{dir}/**/*");
            foreach (var item in items)
            {
                command.Validate(Path.Combine(dir, item, item));
            }
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }
}