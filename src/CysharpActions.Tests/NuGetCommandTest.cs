namespace CysharpActions.Tests;

[Collection("Console output")]
public class NuGetCommandTest
{
    [Fact]
    public async Task PushAsyncDryRunRedactsApiKeyTest()
    {
        var dir = $".tests/{nameof(NuGetCommandTest)}/{nameof(PushAsyncDryRunRedactsApiKeyTest)}";
        var files = Enumerable.Range(0, 3)
            .SelectMany(x => new[] { $"foo{x}.nupkg", $"foo{x}.snupkg" })
            .ToArray();
        const string apiKey = "secret-api-key-must-not-be-logged";
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            CreateFiles(dir, files);
            Console.SetOut(output);

            var command = new NuGetCommand(apiKey, true);
            await command.PushAsync(files.Select(x => Path.Combine(dir, x)));

            var log = output.ToString();
            Assert.DoesNotContain(apiKey, log, StringComparison.Ordinal);
            Assert.Contains("-k ***", log, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
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
