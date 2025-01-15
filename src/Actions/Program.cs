﻿using Actions;
using Actions.Commands;
using Actions.Contexts;
using Actions.Utils;
using Cysharp.Diagnostics;
using System.Runtime.CompilerServices;
using static Actions.Utils.ZxHelper;

var app = ConsoleApp.Create();
app.Add<ActionsBatch>();
app.Run(args);

namespace Actions
{
    public class ActionsBatch
    {
#pragma warning disable CA1822 // Mark members as static

        /// <summary>Get version string by versionIncrement. If tag is 1.0.0 and Patch is selected 1.0.1 will be return, Minor will be 1.1.0 and Major will be 2.0.0</summary>
        /// <param name="tag">version string. ex) 1.0.0</param>
        /// <param name="prefix">version prefix to add. ex) v will be v1.0.0</param>
        /// <param name="versionIncrement">Specify increment mode. Available Values wil be Major, Minor and Patch</param>
        /// <param name="isPrerelease">Specify if version is PreRelease</param>
        /// <param name="prerelease">Prerelease suffix</param>
        /// <param name="outputFormat">Select how to output. Console or GitHubActions Output</param>
        [Command("versioning")]
        public void Versioning(string tag, string prefix = "", VersionIncrement versionIncrement = VersionIncrement.Patch, bool isPrerelease = false, string prerelease = "preview", bool withoutPrefix = false, OutputFormatType outputFormat = OutputFormatType.Console)
        {
            var command = new VersioningCommand(tag, prefix, versionIncrement, isPrerelease, prerelease);
            var versioning = command.Versioning(withoutPrefix);

            var output = OutputFormat("version", versioning, outputFormat);
            WriteLog(output);
        }

        /// <summary>
        /// Validate Tag and remove v prefix if exists
        /// </summary>
        /// <param name="tag">version string. ex) 1.0.0 OR v1.0.0</param>
        /// <param name="requireValidation">Set true to exit 1 on fail. Set false to exit 0 even fail.</param>
        /// <returns></returns>
        [ConsoleAppFilter<GitHubCliFilter>]
        [Command("validate-tag")]
        public async Task ValidateTag(string tag, bool requireValidation)
        {
            var command = new ValidateTagCommand();
            var normalizedTag = command.Normalize(tag);
            await command.ValidateTagAsync(normalizedTag);

            GitHubOutput("tag", tag);
            GitHubOutput("normalized-tag", normalizedTag);
        }

        /// <summary>
        /// Update Version for specified path and commit.
        /// </summary>
        /// <param name="version">version string. ex) 1.0.0</param>
        /// <param name="pathString">string (./package.json) and NewLine deliminated strings (./package.json\n./plugin.cfg).</param>
        /// <param name="dryRun">dryRun mode not changes actual file but shows plan.</param>
        /// <remarks>
        /// Because GitHub Actions workflow dispatch passes arguments as string, you need to split path by NewLine. It means use `string[] pathString` is un-natural for GitHub Actions.
        /// </remarks>
        [ConsoleAppFilter<GitHubContextFilter>]
        [Command("update-version")]
        public async Task<int> UpdateVersion(string version, string pathString, bool dryRun)
        {
            // update version
            var command = new UpdateVersionCommand(version);
            var paths = SplitByNewLine(pathString);
            foreach (var path in paths)
            {
                WriteLog($"Update begin, {path} ...");
                if (string.IsNullOrWhiteSpace(path))
                {
                    WriteLog("Empty path detected, skip execution.");
                    continue;
                }

                using (_ = new GitHubActionsGroupLogger($"Before, {path}"))
                    WriteLog(File.ReadAllText(path));
                var result = command.UpdateVersion(path, dryRun);
                using (_ = new GitHubActionsGroupLogger($"After, {path}"))
                    WriteLog(result.After);
            }

            // Git Commit
            using (_ = new GitHubActionsGroupLogger("git commit changes"))
            {
                await GitCommitAsync(dryRun, version);
            }

            WriteLog($"Completed ...");

            return 0;
        }

        /// <summary>
        /// Validate specified path contains file
        /// </summary>
        /// <param name="pathPatternString">Glob style path pattern(./nuget/*.nupkg) and NewLine deliminated strings (./nuget/*.nupkg\n./nuget/*.snupkg).</param>
        [Command("validate-file-exists")]
        public void ValidateFileExists(string pathPatternString)
        {
            var pathPatterns = SplitByNewLine(pathPatternString);
            foreach (var pathPattern in pathPatterns)
            {
                using var _ = new GitHubActionsGroupLogger($"Validating path, {pathPattern}");
                {
                    WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(pathPattern)}");
                    var command = new FileExsistsCommand();
                    command.Validate(pathPattern);
                }
            }

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Validate specified path contains nuget packages
        /// </summary>
        /// <param name="pathPatternString">Glob style path pattern(./nuget/*.nupkg) and NewLine deliminated strings (./nuget/*.nupkg\n./nuget/*.snupkg).</param>
        [Command("validate-nupkg-exists")]
        public void ValidateNupkgExists(string pathPatternString)
        {
            var pathPatterns = SplitByNewLine(pathPatternString);
            foreach (var pathPattern in pathPatterns)
            {
                using var _ = new GitHubActionsGroupLogger($"Validating path, {pathPattern}");
                WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(pathPattern)}");

                var fileName = Path.GetFileName(pathPattern);
                if (string.IsNullOrWhiteSpace(pathPattern))
                {
                    WriteLog("Empty path detected, skip execution.");
                    continue;
                }

                var allowMissing = Path.GetExtension(fileName) == ".snupkg";
                if (allowMissing)
                {
                    WriteLog(".snupkg detected, allow missing file.");
                }

                var command = new FileExsistsCommand(allowMissing);
                command.Validate(pathPattern);
            }

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Create Release
        /// </summary>
        /// <param name="tag">version string. ex) 1.0.0</param>
        /// <param name="releaseTitle">Release title</param>
        /// <param name="releaseAssetPathString">Release assets to upload</param>
        [ConsoleAppFilter<GitHubContextFilter>]
        [Command("create-release")]
        public async void CreateRelease(string tag, string releaseTitle, string releaseAssetPathString)
        {
            var releaseAssets = SplitByNewLine(releaseAssetPathString);

            var command = new CreateReleaseCommand(tag, releaseTitle);
            await command.CreateReleaseAsync();

            WriteLog($"Uploading assets ...");
            await command.UploadAssetFiles(tag, releaseAssets);

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Create dummy files
        /// </summary>
        /// <param name="basePath"></param>
        [Command("create-dummy")]
        public void CreateDummy(string basePath)
        {
            WriteLog($"Creating dummy files, under {basePath} ...");

            var command = new CreateDummyCommand();
            command.CreateDummy(basePath);

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// debug command
        /// </summary>
        /// <returns></returns>
        [Command("debug")]
        public void Debug(string[] foo)
        {
            Console.WriteLine($"--foo {string.Join(",", foo)}");
        }

        /// <summary>
        /// Git Commit
        /// </summary>
        /// <param name="dryRun"></param>
        /// <param name="tag"></param>
        /// <param name="email"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task GitCommitAsync(bool dryRun, string tag, string email = "41898282+github-actions[bot]@users.noreply.github.com", string user = "github-actions[bot]")
        {
            WriteLog($"Checking File change has been happen ...");
            var commited = "0";
            var branchName = "";
            var isBranchCreated = "false";
            try
            {
                var result = await "git diff --exit-code"; // 0 = no diff, 1 = diff
                WriteLog("git diff not found, skipping commit.");
            }
            catch (ProcessErrorException)
            {
                WriteLog("Detected git diff.");
                if (dryRun)
                {
                    WriteLog("Dryrun Mode detected, creating branch and switch.");
                    branchName = $"test-release/{tag}";
                    isBranchCreated = "true";
                    await $"git switch -c {branchName}";
                }

                WriteLog("Committing change. Running following.");
                await $"git config --local user.email \"{email}\"";
                await $"git config --local user.name \"{user}\"";
                await $"git commit -a -m \"{EscapeArg($"feat: Update package.json to {tag}")}\" -m \"{EscapeArg($"Commit by [GitHub Actions]({GitHubContext.Current.WorkflowRunUrl})")}\"";

                commited = "1";
            }

            var sha = await "git rev-parse HEAD";
            GitHubOutput("commited", commited);
            GitHubOutput("sha", sha);
            GitHubOutput("branch-name", branchName);
            GitHubOutput("is-branch-created", isBranchCreated);
        }

#pragma warning restore CA1822 // Mark members as static

        private static string OutputFormat(string key, string value, OutputFormatType format) => format switch
        {
            OutputFormatType.Console => value,
            OutputFormatType.GitHubActionsOutput => $"{key}={value}",
            _ => throw new NotImplementedException(nameof(format)),
        };

        private static string[] SplitByNewLine(string stringsValue) => stringsValue.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        private static void WriteLog(string value) => Console.WriteLine($"[{DateTime.Now:s}] {value}");
        private static void WriteError(string value) => Console.WriteLine($"[{DateTime.Now:s}] ERROR: {value}");

        private static void WriteVerbose(string value)
        {
            if (ActionsBatchOptions.Verbose)
            {
                WriteLog(value);
            }
        }

        private static void GitHubOutput(string key, string value, [CallerMemberName] string? callerMemberName = null)
        {
            var input = $"{key}={value}";
            var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT", EnvironmentVariableTarget.Process) ?? Path.Combine(Directory.GetCurrentDirectory(), $"GitHubOutputs/{callerMemberName}");
            if (!Directory.Exists(Path.GetDirectoryName(output)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            }

            WriteLog($"GitHub Output: {input}");
            File.AppendAllLines(output, [input]);
        }
    }

    internal class GitHubCliFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
    {
        public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
        {
            // Ensure GH CLI can access on CI.
            if (Environment.GetEnvironmentVariable("CI") is not null)
            {
                _ = Environment.GetEnvironmentVariable("GH_REPO") ?? throw new ActionCommandException("Environment Variable 'GH_REPO' is required");
                _ = Environment.GetEnvironmentVariable("GH_TOKEN") ?? throw new ActionCommandException("Environment Variable 'GH_TOKEN' is required");
            }
            await Next.InvokeAsync(context, cancellationToken);
        }
    }

    internal class GitHubContextFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
    {
        public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
        {
            if (!(context.Arguments.Contains("-h") || context.Arguments.Contains("--help")))
            {
                _ = Environment.GetEnvironmentVariable("GITHUB_CONTEXT") ?? throw new ArgumentNullException("Environment Variable 'GITHUB_CONTEXT' is required");
            }
            await Next.InvokeAsync(context, cancellationToken);
        }
    }

    public class ActionCommandException(string message, Exception? innterException = null) : Exception(message, innterException);

    internal static class ActionsBatchOptions
    {
        public static readonly bool Verbose = bool.Parse(Environment.GetEnvironmentVariable("ACTIONS_BATCH_OPTIONS_VERBOSE") ?? "false");
        public static readonly string? GitHubContext = Environment.GetEnvironmentVariable("GITHUB_CONTEXT");
    }
}