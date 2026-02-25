#pragma warning disable CA1822 // Mark members as static
using CysharpActions;
using CysharpActions.Commands;
using CysharpActions.Contexts;
using CysharpActions.Utils;

var app = ConsoleApp.Create();
app.UseFilter<GlobalCompleteLogFilter>();
app.Add<ActionsBatch>();
app.Run(args);

namespace CysharpActions
{
    public class ActionsBatch
    {
        // Clean package.json branch

        /// <summary>
        /// Delete Git branch
        /// </summary>
        /// <param name="branch">branch name to delete</param>
        /// <returns></returns>
        [ConsoleAppFilter<GitHubContextFilter>]
        [ConsoleAppFilter<GitHubCliFilter>]
        [Command("delete-branch")]
        public async Task DeleteBranch(string branch)
        {
            var command = new GitCommand();
            var result = await command.DeleteBranchAsync(branch);

            GitHubActions.SetOutput("deleted", result.ToString().ToLower());
        }

        // Update package.json

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
        [ConsoleAppFilter<GitHubCliFilter>]
        [Command("update-version")]
        public async Task UpdateVersion(string version, string pathString, bool dryRun, bool sign = true)
        {
            var paths = pathString.ToMultiLine();
            if (!paths.Any())
                throw new ActionCommandException("No path specified.");

            // update version
            var command = new UpdateVersionCommand(version);
            command.UpdateVersions(paths, dryRun);

            // Git Commit
            using (_ = GitHubActions.StartGroup("git commit changes"))
            {
                var gitCommand = new GitCommand();
                var (commited, sha, branchName, isBranchCreated) = sign 
                    ? await gitCommand.CommitWithSignAsync(dryRun, version) 
                    : await gitCommand.CommitAsync(dryRun, version);

                GitHubActions.SetOutput("commited", commited ? "1" : "0");
                GitHubActions.SetOutput("sha", sha);
                GitHubActions.SetOutput("branch-name", branchName);
                GitHubActions.SetOutput("is-branch-created", isBranchCreated);
            }
        }

        /// <summary>
        /// Get New Version string by incrementing specified version.
        /// </summary>
        /// <param name="version">version string. ex) 1.0.0</param>
        /// <param name="type">version increment type. ex) Patch will generate 1.0.1</param>
        /// <param name="prefix">prefix string. ex) v</param>
        /// <param name="suffix">suffix string. ex) -dev</param>
        /// <remarks>
        /// Because GitHub Actions workflow dispatch passes arguments as string, you need to split path by NewLine. It means use `string[] pathString` is un-natural for GitHub Actions.
        /// </remarks>
        [ConsoleAppFilter<GitHubContextFilter>]
        [Command("increment-version")]
        public void IncrementVersion(string version, VersionIncrement type, string prefix = "", string suffix = "")
        {
            GitHubActions.WriteLog($"Showing inputs. version: {version}, versionIncrement: {type}, prefix: {prefix}, suffix: {suffix}");
            var command = new VersioningCommand(prefix, suffix);
            var newVersion = command.UpdateVersion(version, type);
            GitHubActions.SetOutput("version", newVersion);
        }

        // Benchmark

        /// <summary>
        /// Generate GitHub Actions Matrix JSON from benchmark loader config
        /// </summary>
        /// <param name="benchmarkNamePrefix">Benchmark name prefix</param>
        /// <param name="configPath">Benchmark loader yaml file path</param>
        /// <param name="branch">Branch name to run the benchmark, required when config type is not loader</param>
        [Command("benchmark-loader2matrix")]
        public void BenchmarkLoader2Matrix(string benchmarkNamePrefix, string configPath, string? branch = null)
        {
            var command = new BenchmarkLoader2MatrixCommand(benchmarkNamePrefix, configPath, branch);
            var json = command.GenerateMatrix();
            var prettyJson = BenchmarkLoader2MatrixCommand.ToPrettyPrint(json);

            GitHubActions.SetOutput("matrix", json);
            GitHubActions.WriteLog($"Pretty print Matrix json for debug:\n{prettyJson}");
        }

        /// <summary>
        /// Create GitHub Matrix JSON from benchmark config YAML file
        /// </summary>
        /// <param name="configPath">Benchmark config yaml file path</param>
        [Command("benchmark-config2matrix")]
        public void BenchmarkConfig2Matrix(string configPath)
        {
            var command = new BenchmarkConfig2MatrixCommand(configPath);
            var json = command.GenerateMatrix();
            var prettyJson = BenchmarkConfig2MatrixCommand.ToPrettyPrint(json);

            GitHubActions.SetOutput("matrix", json);
            GitHubActions.WriteLog($"Pretty print Matrix json for debug:\n{prettyJson}");
        }

        // Create Release

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
            var command = new ValidateTagCommand(new GitHubReleaseExeGh());
            var normalizedTag = command.Normalize(tag);
            if (requireValidation)
            {
                await command.ValidateTagAsync(normalizedTag);
            }

            GitHubActions.SetOutput("tag", tag);
            GitHubActions.SetOutput("normalized-tag", normalizedTag);
        }

        /// <summary>
        /// Validate specified path contains file
        /// </summary>
        /// <param name="pathPatternString">Glob style path pattern(./nuget/*.nupkg) and NewLine deliminated strings (./nuget/*.nupkg\n./nuget/*.snupkg).</param>
        [Command("validate-file-exists")]
        public void ValidateFileExists(string pathPatternString)
        {
            var pathPatterns = pathPatternString.ToMultiLine();

            var command = new FileExsistsCommand();
            command.ValidateAssetPath(pathPatterns);
        }

        /// <summary>
        /// Validate specified path contains nuget packages
        /// </summary>
        /// <param name="pathPatternString">Glob style path pattern(./nuget/*.nupkg) and NewLine deliminated strings (./nuget/*.nupkg\n./nuget/*.snupkg).</param>
        [Command("validate-nupkg-exists")]
        public void ValidateNupkgExists(string pathPatternString)
        {
            var pathPatterns = pathPatternString.ToMultiLine();

            var command = new FileExsistsCommand();
            command.ValidateNuGetPath(pathPatterns);
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
            var releaseAssets = releaseAssetPathString.ToMultiLine();

            var command = new CreateReleaseCommand(tag, releaseTitle);

            GitHubActions.WriteLog($"Creating Release ...");
            await command.CreateReleaseAsync();

            GitHubActions.WriteLog($"Uploading {releaseAssets.Length} assets ...");
            await command.UploadAssetFilesAsync(releaseAssets);
        }

        /// <summary>
        /// Push NuGet Package
        /// </summary>
        /// <param name="nugetPathString">NuGet Packages to push.</param>
        /// <param name="apiKey">NuGet API Key</param>
        /// <param name="dryRun">Dry run or not</param>
        /// <returns></returns>
        [Command("nuget-push")]
        public async Task NuGetPush(string nugetPathString, string apiKey, bool dryRun)
        {
            var nugetPaths = nugetPathString.ToMultiLine();

            GitHubActions.WriteLog($"Uploading {nugetPaths.Length} nuget packages (dryRun: {dryRun})...");
            var command = new NuGetCommand(apiKey, dryRun);
            await command.PushAsync(nugetPaths);
        }

        /// <summary>
        /// Create dummy files
        /// </summary>
        /// <param name="basePath"></param>
        [Command("create-dummy")]
        public async Task CreateDummy(string basePath)
        {
            GitHubActions.WriteLog($"Creating dummy files, under {basePath} ...");

            var command = new CreateDummyCommand();
            command.CreateDummy(basePath);
        }
    }

    internal class GlobalCompleteLogFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
    {
        public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
        {
            // don't show log on --help and --version
            var isMetaCommand = context.Arguments.Contains("--help") || context.Arguments.Contains("-h") || context.Arguments.Contains("--version");
            if (!isMetaCommand)
            {
                GitHubActions.WriteLog($"Begin {context.CommandName} command ...");
            }
            await Next.InvokeAsync(context, cancellationToken);
            if (!isMetaCommand)
            {
                GitHubActions.WriteLog($"Completed ({Environment.ExitCode}) ...");
            }
        }
    }

    internal class GitHubCliFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
    {
        public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
        {
            // Ensure GH CLI can access on CI.
            if (GitHubEnv.Current.CI)
            {
                GitHubActions.WriteLog($"Validating gh cli environment variables ...");

                _ = GHEnv.Current.GH_REPO ?? throw new ActionCommandException("Environment Variable 'GH_REPO' is required");
                _ = GHEnv.Current.GH_TOKEN ?? throw new ActionCommandException("Environment Variable 'GH_TOKEN' is required");
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
                GitHubActions.WriteLog($"Validating GitHub Context ...");
                // Ensure GitHubContext can be resolved
                _ = GitHubContext.Current;
            }
            await Next.InvokeAsync(context, cancellationToken);
        }
    }

    public class ActionCommandException(string message, Exception? innterException = null) : Exception(message, innterException);

    internal static class ActionsBatchOptions
    {
        public static readonly bool Verbose = GitHubEnv.Current.ACTIONS_STEP_DEBUG || GitHubEnv.Current.RUNNER_DEBUG;
    }
}
