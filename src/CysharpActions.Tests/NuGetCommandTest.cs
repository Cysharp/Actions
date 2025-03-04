namespace CysharpActions.Tests;

public class NuGetCommandTest
{
    [Fact]
    public async Task PushAsyncDryrunTest()
    {
        var dir = $".tests/{nameof(NuGetCommandTest)}/{nameof(PushAsyncDryrunTest)}";
        var files = Enumerable.Range(0, 3)
            .SelectMany(x => new[] { $"foo{x}.nupkg", $"foo{x}.snupkg" })
            .ToArray();
        try
        {
            CreateFiles(dir, files);
            var command = new NuGetCommand("", true);
            await command.PushAsync(files.Select(x => Path.Combine(dir, x)));
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task GlobPushAsyncDryrunTest()
    {
        var dir = $".tests/{nameof(NuGetCommandTest)}/{nameof(GlobPushAsyncDryrunTest)}";
        var files = Enumerable.Range(0, 3)
            .SelectMany(x => new[] { $"foo{x}.nupkg", $"foo{x}.snupkg" })
            .ToArray();
        try
        {
            CreateFiles(dir, files);
            var command = new NuGetCommand("", true);
            await command.PushAsync(files.Select(x => Path.Combine(dir, "**", x)));
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }
}
