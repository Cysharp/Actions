namespace CysharpActions.Tests;

public class FileExistsCommandTest
{
    [Fact]
    public void SkipEmptyPathTest()
    {
        var command = new FileExsistsCommand();
        command.ValidateAssetPath([""]);
        command.ValidateNuGetPath([""]);
    }

    [Fact]
    public void ThrowMissingSnupkgTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(ThrowMissingSnupkgTest)}";
        var snupkgPaths = new[] { "foo", "bar", "piyo" }
            .Select(x => Path.Combine(dir, x));
        var snupkgGlobPatterns = new[] { "foo", "bar", "piyo" }
            .Select(x => Path.Combine(dir, "**", x));

        // .snupkg allows file not found, test should pass
        var command = new FileExsistsCommand();
        Assert.Throws<ActionCommandException>(() => command.ValidateNuGetPath(snupkgPaths));
        Assert.Throws<ActionCommandException>(() => command.ValidateNuGetPath(snupkgGlobPatterns));
    }

    [Fact]
    public void AllowMissingSnupkgTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(AllowMissingSnupkgTest)}";
        var snupkgPaths = new[] { "foo.snupkg", "bar.snupkg", "piyo.snupkg" }
            .Select(x => Path.Combine(dir, x));
        var snupkgGlobPatterns = new[] { "foo.snupkg", "bar.snupkg", "piyo.snupkg" }
            .Select(x => Path.Combine(dir, "**", x));

        // .snupkg allows file not found, test should pass
        var command = new FileExsistsCommand();
        command.ValidateNuGetPath(snupkgPaths);
        command.ValidateNuGetPath(snupkgGlobPatterns);
    }

    [Fact]
    public void FullPathTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(FullPathTest)}";
        var items = new[] { "dummy.nupkg", "dummy.txt" };
        var paths = items.Select(x => Path.Combine(dir, x));
        try
        {
            CreateFiles(dir, items, false);
            var command = new FileExsistsCommand();
            command.ValidateAssetPath(paths);
            command.ValidateNuGetPath(paths);
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public void GlobPatternTest()
    {
        var dir = $".tests/{nameof(FileExistsCommandTest)}/{nameof(GlobPatternTest)}";
        var items = new[] { "foo", "bar", "piyo", "test.txt", "hoge.txt" };
        try
        {
            CreateFiles(dir, items, false);
            var command = new FileExsistsCommand();

            // recursive glob
            {
                var path = $"{dir}/**/*";
                command.ValidateAssetPath([path]);
                command.ValidateNuGetPath([path]);
            }

            // wildcard glob
            foreach (var item in items)
            {
                // wildcard file
                {
                    var path = $"{dir}/*";
                    command.ValidateAssetPath([path]);
                    command.ValidateNuGetPath([path]);
                }
                {
                    var path = $"{dir}/*.txt";
                    command.ValidateAssetPath([path]);
                    command.ValidateNuGetPath([path]);
                }
                {
                    var path = $"{dir}/hoge.*";
                    command.ValidateAssetPath([path]);
                    command.ValidateNuGetPath([path]);
                }
                {
                    var path = $"{dir}/**/hoge.*";
                    command.ValidateAssetPath([path]);
                    command.ValidateNuGetPath([path]);
                }

                // wildcard directory
                {
                    var path = $"{Path.GetDirectoryName(dir)}/*/{item}";
                    command.ValidateAssetPath([path]);
                    command.ValidateNuGetPath([path]);
                }
                {
                    var path = $"{dir}/**/{item}";
                    command.ValidateAssetPath([path]);
                    command.ValidateNuGetPath([path]);
                }

                // not found
                {
                    var path = $"{dir}/*/{item}";
                    Assert.Throws<ActionCommandException>(() => command.ValidateAssetPath([path]));
                    Assert.Throws<ActionCommandException>(() => command.ValidateNuGetPath([path]));
                }
            }
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }
}