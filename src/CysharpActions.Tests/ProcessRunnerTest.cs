using Cysharp.Diagnostics;
using CysharpActions.Runtime;

namespace CysharpActions.Tests;

[Collection("Git environment")]
public class ProcessRunnerTest
{
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

        Assert.Equal(0, result.ExitCode);
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

        Assert.Equal(0, result.ExitCode);
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
