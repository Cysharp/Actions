using CysharpActions;
using CysharpActions.Utils;
using System.Diagnostics;

namespace CysharpActions.Tests;

public class CliContractTest
{
    [Fact]
    public async Task HelpListsPublicCommandsTest()
    {
        var result = await RunCliAsync(["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [
                "benchmark-config2matrix",
                "benchmark-loader2matrix",
                "create-dummy",
                "create-release",
                "delete-branch",
                "increment-version",
                "nuget-push",
                "update-version",
                "validate-file-exists",
                "validate-nupkg-exists",
                "validate-tag",
            ],
            ParseCommands(result.Stdout));
    }

    [Fact]
    public async Task UpdateVersionHelpListsPublicOptionsTest()
    {
        var result = await RunCliAsync(["update-version", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--version <string>", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--path-string <string>", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--dry-run", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--additional-commit-path-string <string>", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--sign <bool>", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncrementVersionWritesGitHubOutputTest()
    {
        var baseDirectory = Path.GetFullPath($".tests/{nameof(CliContractTest)}/{nameof(IncrementVersionWritesGitHubOutputTest)}");
        var outputPath = Path.Combine(baseDirectory, "github-output.txt");
        try
        {
            Directory.CreateDirectory(baseDirectory);

            var result = await RunCliAsync(
                ["increment-version", "--version", "1.2.3", "--type", "patch"],
                new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = outputPath });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("version=1.2.4", File.ReadAllText(outputPath).TrimEnd());
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public async Task InvalidVersionReturnsNonZeroExitCodeTest()
    {
        var result = await RunCliAsync(["increment-version", "--version", "invalid", "--type", "patch"]);

        Assert.NotEqual(0, result.ExitCode);
    }

    private static string[] ParseCommands(string help)
    {
        return help.ToMultiLine()
            .SkipWhile(x => x != "Commands:")
            .Skip(1)
            .Where(x => x.StartsWith("  ", StringComparison.Ordinal))
            .Select(x => x.TrimStart().Split(' ', 2)[0])
            .ToArray();
    }

    private static async Task<CliResult> RunCliAsync(string[] arguments, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(typeof(ActionsBatch).Assembly.Location);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start CysharpActions CLI.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CliResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private readonly record struct CliResult(int ExitCode, string Stdout, string Stderr);
}
