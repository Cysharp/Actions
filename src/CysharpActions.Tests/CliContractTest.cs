using CysharpActions;
using CysharpActions.Utils;
using System.Diagnostics;
using System.Text.Json;

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
                "scan-pr-unicode",
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

    [Fact]
    public async Task UnicodePolicyViolationReturnsNonZeroWithoutStackTraceTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(CliContractTest)}/{nameof(UnicodePolicyViolationReturnsNonZeroWithoutStackTraceTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await RunGitAsync(directory, "init");
            await RunGitAsync(directory, "config", "user.email", "test@example.com");
            await RunGitAsync(directory, "config", "user.name", "Test User");
            await RunGitAsync(directory, "config", "commit.gpgSign", "false");

            var sourcePath = Path.Combine(directory, "Test.cs");
            File.WriteAllText(sourcePath, "class C { }");
            await RunGitAsync(directory, "add", "--", "Test.cs");
            await RunGitAsync(directory, "commit", "-m", "base");
            var baseSha = await RunGitAsync(directory, "rev-parse", "HEAD");

            var forbiddenEscape = "\\" + "u202E";
            File.WriteAllText(sourcePath, "class C { " + forbiddenEscape + " }");
            await RunGitAsync(directory, "add", "--", "Test.cs");
            await RunGitAsync(directory, "commit", "-m", "head");
            var headSha = await RunGitAsync(directory, "rev-parse", "HEAD");

            var eventPath = Path.Combine(directory, "event.json");
            File.WriteAllText(eventPath, JsonSerializer.Serialize(new
            {
                pull_request = new
                {
                    title = "Clean title",
                    body = "Clean body",
                    @base = new { sha = baseSha },
                    head = new { sha = headSha },
                },
            }));

            var result = await RunCliAsync([
                "scan-pr-unicode",
                "--event-path", eventPath,
                "--repository-path", directory,
            ]);
            var output = result.Stdout + result.Stderr;

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Test.cs:1:11: C# Unicode escape: U+202E", output, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(ActionCommandException), output, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(UnicodeScanViolationException), output, StringComparison.Ordinal);
            Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
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

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments[0]} failed: {await stderrTask}");
        return stdout.Trim();
    }

    private readonly record struct CliResult(int ExitCode, string Stdout, string Stderr);
}
