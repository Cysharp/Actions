using CysharpActions;
using CysharpActions.Commands;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using Cysharp.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Actions.Core.Extensions;
using Actions.Core.Services;
using System.Collections;

await using var serviceProvider = new ServiceCollection()
    .AddGitHubActionsCore()
    .BuildServiceProvider();
ConsoleApp.ServiceProvider = serviceProvider;

var app = ConsoleApp.Create();
app.Add<ActionsBatch>();
app.Run(args);

namespace CysharpActions
{
    public class ActionsBatch(ICoreService githubactions)
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
        public async Task ValidateTag(string tag, bool requireValidation = false)
        {
            foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
            {
                WriteLog($"{item.Key} {item.Value}");
            }
            requireValidation = githubactions.GetBoolInput("require-validation", new InputOptions(false));
            var command = new ValidateTagCommand();
            var normalizedTag = command.Normalize(tag);
            if (requireValidation)
            {
                await command.ValidateTagAsync(normalizedTag);
            }

            await githubactions.SetOutputAsync("tag", tag);
            await githubactions.SetOutputAsync("normalized-tag", normalizedTag);
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
        public async Task UpdateVersion(string version, string pathString, bool dryRun = false)
        {
            foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
            {
                WriteLog($"{item.Key} {item.Value}");
            }
            dryRun = githubactions.GetBoolInput("dry-run", new InputOptions(false));
            var command = new UpdateVersionCommand(version);

            // update version
            var paths = pathString.SplitByNewLine();
            foreach (var path in paths)
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(path))
                    throw new ActionCommandException($"Upload target file not found", new FileNotFoundException(path));

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
                var (commited, sha, branchName, isBranchCreated) = await GitCommitAsync(dryRun, version);

                await githubactions.SetOutputAsync("commited", commited ? "1" : "0");
                await githubactions.SetOutputAsync("sha", sha);
                await githubactions.SetOutputAsync("branch-name", branchName);
                await githubactions.SetOutputAsync("is-branch-created", isBranchCreated);
            }

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Validate specified path contains file
        /// </summary>
        /// <param name="pathPatternString">Glob style path pattern(./nuget/*.nupkg) and NewLine deliminated strings (./nuget/*.nupkg\n./nuget/*.snupkg).</param>
        [Command("validate-file-exists")]
        public void ValidateFileExists(string pathPatternString)
        {
            var pathPatterns = pathPatternString.SplitByNewLine();
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
            var pathPatterns = pathPatternString.SplitByNewLine();
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
        [ConsoleAppFilter<GitHubCliFilter>]
        [Command("create-release")]
        public async Task CreateRelease(string tag, string releaseTitle, string releaseAssetPathString)
        {
            var releaseAssets = releaseAssetPathString.SplitByNewLine();

            var command = new CreateReleaseCommand(tag, releaseTitle);

            WriteLog($"Creating Release ...");
            await command.CreateReleaseAsync();

            if (releaseAssets.Length > 0)
            {
                WriteLog($"Uploading assets ...");
                await command.UploadAssetFiles(releaseAssets);
            }

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Create dummy files
        /// </summary>
        /// <param name="basePath"></param>
        [Command("create-dummy")]
        public async Task CreateDummy(string basePath)
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
        private async Task<(bool commited, string sha, string branchName, string isBranchCreated)> GitCommitAsync(bool dryRun, string tag, string email = "41898282+github-actions[bot]@users.noreply.github.com", string user = "github-actions[bot]")
        {
            WriteLog($"Checking File change has been happen ...");
            var commited = false;
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
                await $"git commit -a -m \"{$"feat: Update package.json to {tag}".EscapeArg()}\" -m \"{$"Commit by [GitHub Actions]({GitHubContext.Current.WorkflowRunUrl})".EscapeArg()}\"";

                commited = true;
            }

            var sha = await "git rev-parse HEAD";
            return (commited, sha, branchName, isBranchCreated);
        }

#pragma warning restore CA1822 // Mark members as static

        private static string OutputFormat(string key, string value, OutputFormatType format) => format switch
        {
            OutputFormatType.Console => value,
            OutputFormatType.GitHubActionsOutput => $"{key}={value}",
            _ => throw new NotImplementedException(nameof(format)),
        };
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