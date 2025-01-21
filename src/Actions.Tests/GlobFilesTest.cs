using Actions.Utils;

namespace Actions.Tests;

public class GlobFilesTest
{
    [Fact]
    public void WildcardFileTest()
    {
        var dir = $".tests/{nameof(GlobFilesTest)}/{nameof(WildcardFileTest)}";
        var items = new[] { "foo", "bar", "piyo", "test.txt", "hoge.txt" };
        try
        {
            CreateFiles(dir, items, false);
            foreach (var item in items)
            {
                foreach (var file in GlobFiles.EnumerateFiles($"{dir}/{item}"))
                {
                    // should be full path
                    File.Exists(file);
                }
            }

            foreach (var item in new[] { $"{dir}/*", $"{dir}/*.txt", $"{dir}/hoge.*", $"{dir}/**/hoge.*", $"{dir}/**/*" })
            {
                foreach (var file in GlobFiles.EnumerateFiles(item))
                {
                    // should be full path
                    File.Exists(file);
                }
            }
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public void WildcardDirectoryTest()
    {
        var dir = $".tests/{nameof(GlobFilesTest)}/{nameof(WildcardDirectoryTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        try
        {
            CreateFiles(dir, items, false);
            foreach (var item in items)
            {
                foreach (var pattern in new[] { $"{Path.GetDirectoryName(dir)}/*/{item}", $"{dir}/{item}", $"{dir}/**/{item}", $"{dir}/**/*", $"{dir}/*/{item}" })
                {
                    foreach (var file in GlobFiles.EnumerateFiles(pattern))
                    {
                        // should be full path
                        File.Exists(file);
                    }
                }
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
        var dir = $".tests/{nameof(GlobFilesTest)}/{nameof(RecursiveWildcardDirectoryTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        try
        {
            CreateFiles(dir, items, false);
            foreach (var item in items)
            {
                foreach (var pattern in new[] { $"{Path.GetDirectoryName(dir)}/**/{item}", $"{dir}/**/{item}" })
                {
                    foreach (var file in GlobFiles.EnumerateFiles(pattern))
                    {
                        // should be full path
                        File.Exists(file);
                    }
                }
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
        var dir = $".tests/{nameof(GlobFilesTest)}/{nameof(RecursiveWildcardDirectoryAndFileTest)}";
        var items = new[] { "foo", "bar", "piyo" };
        try
        {
            CreateFiles(dir, items, true);
            foreach (var file in GlobFiles.EnumerateFiles($"{dir}/**/*"))
            {
                // should be full path
                File.Exists(file);
            }
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }
}