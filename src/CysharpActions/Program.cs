#pragma warning disable CA1822 // Mark members as static
using CysharpActions;
using CysharpActions.Commands;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using Cysharp.Diagnostics;

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
        public async Task UpdateVersion(string version, string pathString, bool dryRun)
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
                var (commited, sha, branchName, isBranchCreated) = await GitCommitAsync(dryRun, version);

                GitHubActions.SetOutput("commited", commited ? "1" : "0");
                GitHubActions.SetOutput("sha", sha);
                GitHubActions.SetOutput("branch-name", branchName);
                GitHubActions.SetOutput("is-branch-created", isBranchCreated);
            }
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

        /// <summary>
        /// Git Commit
        /// </summary>
        /// <param name="dryRun"></param>
        /// <param name="tag"></param>
        /// <param name="email"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<(bool commited, string sha, string branchName, string isBranchCreated)> GitCommitAsync(bool dryRun, string tag)
        {
            Env.useShell = false;

            GitHubActions.WriteLog($"Set git user.email/user.name if missing ...");
            await GitHelper.SetGitUserEmailAsync();

            GitHubActions.WriteLog($"Checking File change has been happen ...");
            var commited = false;
            var branchName = "";
            var isBranchCreated = "false";
            try
            {
                var result = await "git diff --exit-code"; // 0 = no diff, 1 = diff
                GitHubActions.WriteLog("git diff not found, skipping commit.");
            }
            catch (ProcessErrorException)
            {
                GitHubActions.WriteLog("Detected git diff.");
                if (dryRun)
                {
                    GitHubActions.WriteLog("Dryrun Mode detected, creating branch and switch.");
                    branchName = $"test-release/{tag}";
                    isBranchCreated = "true";
                    await $"git switch -c {branchName}";
                }

                GitHubActions.WriteLog("Committing change. Running following.");
                await $"git commit -a -m \"{$"feat: Update package.json to {tag}"}\" -m \"{$"Commit by [GitHub Actions]({GitHubContext.Current.WorkflowRunUrl})"}\"";

                commited = true;
            }

            var sha = await "git rev-parse HEAD";
            return (commited, sha, branchName, isBranchCreated);
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
                GitHubActions.WriteLog($"Completed {(Environment.ExitCode)}...");
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