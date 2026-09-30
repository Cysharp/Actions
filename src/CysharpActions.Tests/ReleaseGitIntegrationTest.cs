using CysharpActions.Runtime;
using System.Diagnostics;

namespace CysharpActions.Tests;

public class ReleaseGitIntegrationTest
{
    [Theory]
    [InlineData("new")]
    [InlineData("lightweight")]
    [InlineData("annotated")]
    [InlineData("mismatch")]
    [InlineData("race")]
    public async Task RealGitTagsAndLeases(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "release-git-" + Guid.NewGuid().ToString("N"));
        var work = Path.Combine(root, "work");
        var bare = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(work);
        const string tag = "v1.2.3";
        var credentials = new GitHubCredentials("Cysharp/Actions", "test-token");
        var state = Path.Combine(root, "state.json");
        var releaseCreated = false;
        try
        {
            await Git("init", "--bare", bare);
            await Git("init");
            await Git("config", "user.name", "Test");
            await Git("config", "user.email", "test@example.com");
            await Git("config", "commit.gpgSign", "false");
            await Git("config", "tag.gpgSign", "false");
            await Git("commit", "--allow-empty", "-m", "initial");
            await Git("remote", "add", "origin", bare);
            if (scenario is "lightweight" or "annotated" or "mismatch")
            {
                if (scenario == "annotated") await Git("tag", "-a", tag, "-m", "Annotated tag");
                else await Git("tag", tag);
                await Git("push", "origin", $"refs/tags/{tag}");
            }
            if (scenario == "mismatch") await Git("commit", "--allow-empty", "-m", "different build");
            var command = new ReleaseLifecycleCommand(Run);
            if (scenario is "mismatch" or "race")
            {
                await Assert.ThrowsAnyAsync<Exception>(() => command.CreateAsync(tag, "title", state, credentials, TestContext.Current.CancellationToken));
                Assert.False(releaseCreated);
                await command.CleanupAsync(state, credentials, TestContext.Current.CancellationToken);
                Assert.NotEmpty((await Git("ls-remote", "--tags", "origin")).Stdout);
            }
            else
            {
                await command.CreateAsync(tag, "title", state, credentials, TestContext.Current.CancellationToken);
                Assert.True(releaseCreated);
                await command.CleanupAsync(state, credentials, TestContext.Current.CancellationToken);
                var remaining = (await Git("ls-remote", "--tags", "origin")).Stdout;
                if (scenario == "new") Assert.Empty(remaining);
                else Assert.Contains($"refs/tags/{tag}", remaining);
            }
        }
        finally
        {
            // Git objects may be read-only on Windows.
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }

        async Task<ProcessResult> Run(CommandSpec spec, CancellationToken cancellationToken)
        {
            if (spec.FileName == "git")
            {
                // Keep the isolated local origin instead of installing GitHub credentials.
                if (spec.Arguments.Take(2).SequenceEqual(new[] { "remote", "set-url" }))
                    return new("", "");
                if (scenario == "race" && spec.Arguments[0] == "push")
                {
                    // Another actor creates the same tag at the same commit after our absence check.
                    await Git("push", "origin", $"HEAD:refs/tags/{tag}");
                }
                return await Git(spec.Arguments.ToArray());
            }
            if (spec.Arguments.Contains("--slurp")) return new("[[]]", "");
            if (spec.Arguments.Contains("POST")) { releaseCreated = true; return new("{\"id\":42}", ""); }
            if (spec.Arguments.Contains("DELETE")) return new("", "");
            return new($"{{\"id\":42,\"tag_name\":\"{tag}\",\"draft\":true}}", "");
        }

        async Task<ProcessResult> Git(params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = work,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            var result = new ProcessResult((await stdout).Trim(), (await stderr).Trim());
            if (process.ExitCode != 0) throw new InvalidOperationException(result.Stderr);
            return result;
        }
    }
}
