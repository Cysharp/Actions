namespace CysharpActions.Tests;

[Collection("Console output")]
public class NuGetCommandTest
{
    [Fact]
    public async Task PushAsyncPassesStructuredCommandToRunnerTest()
    {
        var dir = $".tests/{nameof(NuGetCommandTest)}/{nameof(PushAsyncPassesStructuredCommandToRunnerTest)}";
        var path = Path.Combine(dir, "package with spaces.nupkg");
        const string apiKey = "secret-api-key";
        CysharpActions.Runtime.CommandSpec? actual = null;
        CancellationToken actualCancellationToken = default;
        using var cancellation = new CancellationTokenSource();

        Task<CysharpActions.Runtime.ProcessResult> Run(
            CysharpActions.Runtime.CommandSpec command,
            CancellationToken cancellationToken)
        {
            actual = command;
            actualCancellationToken = cancellationToken;
            return Task.FromResult(new CysharpActions.Runtime.ProcessResult("", ""));
        }

        try
        {
            CreateFile(path, "package");
            await new NuGetCommand(apiKey, false, Run).PushAsync([path], cancellation.Token);

            Assert.NotNull(actual);
            Assert.Equal("dotnet", actual.Value.FileName);
            Assert.Equal(
                ["nuget", "push", path, "--skip-duplicate", "-s", "https://api.nuget.org/v3/index.json", "-k", apiKey],
                actual.Value.Arguments);
            Assert.Contains(7, actual.Value.SecretArguments!);
            Assert.Equal(cancellation.Token, actualCancellationToken);
            Assert.DoesNotContain(apiKey, actual.Value.ToDisplayString(), StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }

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
            await command.PushAsync(files.Select(x => Path.Combine(dir, x)), TestContext.Current.CancellationToken);

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
            await command.PushAsync(files.Select(x => Path.Combine(dir, "**", x)), TestContext.Current.CancellationToken);
        }
        finally
        {
            SafeDeleteDirectory(dir);
        }
    }
}
