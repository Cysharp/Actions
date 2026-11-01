using Cysharp.Diagnostics;
using CysharpActions.Runtime;

namespace CysharpActions.Tests;

[Collection("Git environment")]
public class ProcessRunnerTest
{
    [Theory]
    [InlineData("--version", false)]
    [InlineData("--definitely-not-an-option", true)]
    public async Task RunAsyncLogsRedactedCommandEvenOnFailureTest(string argument, bool fails)
    {
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var command = new CommandSpec("dotnet", [argument], new HashSet<int> { 0 });
            if (fails)
                await Assert.ThrowsAsync<ProcessErrorException>(() => ProcessRunner.RunAsync(command, TestContext.Current.CancellationToken));
            else
                await ProcessRunner.RunAsync(command, TestContext.Current.CancellationToken);

            Assert.Contains("Executing: dotnet ***", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(argument, output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsyncLogsCommandBeforeProcessCanStartTest()
    {
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var executable = "missing-command-" + Guid.NewGuid().ToString("N");
            await Assert.ThrowsAnyAsync<Exception>(() => ProcessRunner.RunAsync(
                new CommandSpec(executable, ["two words"]), TestContext.Current.CancellationToken));

            Assert.Contains($"Executing: {executable} \"two words\"", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ToDisplayStringQuotesArgumentsAndRedactsSecretsTest()
    {
        var command = new CommandSpec(
            "tool path",
            ["plain", "two words", "secret-value", "a\"b"],
            new HashSet<int> { 2 });

        Assert.Equal("\"tool path\" plain \"two words\" *** \"a\\\"b\"", command.ToDisplayString());
        Assert.Equal(command.ToDisplayString(), command.ToString());
    }

    [Fact]
    public void ToDisplayStringRejectsInvalidSecretIndexTest()
    {
        var command = new CommandSpec("tool", ["secret-value"], new HashSet<int> { 1 });

        Assert.Throws<InvalidOperationException>(() => command.ToDisplayString());
    }

    [Fact]
    public void GitHubCommitSpecToStringDoesNotExposeTokenOrContentTest()
    {
        var command = new GitHubCommitSpec(
            "secret-token",
            "owner",
            "repository",
            "main",
            null,
            "message",
            [new GitHubTreeItemSpec("package.json", "100644", "secret-content")]);

        var display = command.ToString();
        Assert.DoesNotContain("secret-token", display, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-content", display, StringComparison.Ordinal);
        Assert.Contains("Token = ***", display, StringComparison.Ordinal);
        Assert.Contains("TreeItems = 1", display, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncExecutesArgumentListWithoutShellTest()
    {
        var result = await ProcessRunner.RunAsync(
            new CommandSpec("dotnet", ["--version"]),
            TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public async Task RunAsyncDoesNotTreatStderrAsFailureTest()
    {
        var command = OperatingSystem.IsWindows()
            ? new CommandSpec("cmd", ["/c", "echo progress 1>&2"])
            : new CommandSpec("/bin/sh", ["-c", "printf progress >&2"]);

        var result = await ProcessRunner.RunAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("progress", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncPreservesProcessXNonZeroExitTest()
    {
        await Assert.ThrowsAsync<ProcessErrorException>(() => ProcessRunner.RunAsync(
            new CommandSpec("dotnet", ["--definitely-not-an-option"]),
            TestContext.Current.CancellationToken));
    }
}
