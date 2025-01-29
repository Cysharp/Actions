using CysharpActions;
using CysharpActions.Commands;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using Cysharp.Diagnostics;

var app = ConsoleApp.Create();
app.Add<ActionsBatch>();
app.Run(args);

namespace CysharpActions
{
    public class ActionsBatch
    {
#pragma warning disable CA1822 // Mark members as static

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
            if (requireValidation)
            {
                await command.ValidateTagAsync(normalizedTag);
            }

            GitHubActions.SetOutput("tag", tag);
            GitHubActions.SetOutput("normalized-tag", normalizedTag);
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
        public async Task UpdateVersion(string version, string pathString, bool dryRun)
        {
            // update version
            var command = new UpdateVersionCommand(version);
            var paths = pathString.ToMultiLine();
            foreach (var path in paths)
            {
                GitHubActions.WriteLog($"Update begin, {path} ...");
                if (string.IsNullOrWhiteSpace(path))
                {
                    GitHubActions.WriteLog("Empty path detected, skip execution.");
                    continue;
                }

                using (_ = new GitHubActionsGroup($"Before, {path}"))
                    GitHubActions.WriteLog(File.ReadAllText(path));
                var result = command.UpdateVersion(path, dryRun);
                using (_ = new GitHubActionsGroup($"After, {path}"))
                    GitHubActions.WriteLog(result.After);
            }

            // Git Commit
            using (_ = new GitHubActionsGroup("git commit changes"))
            {
                var (commited, sha, branchName, isBranchCreated) = await GitCommitAsync(dryRun, version);

                GitHubActions.SetOutput("commited", commited ? "1" : "0");
                GitHubActions.SetOutput("sha", sha);
                GitHubActions.SetOutput("branch-name", branchName);
                GitHubActions.SetOutput("is-branch-created", isBranchCreated);
            }
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
                GitHubActions.WriteLog($"Uploading assets ...");
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
        private async Task<(bool commited, string sha, string branchName, string isBranchCreated)> GitCommitAsync(bool dryRun, string tag, string email = "41898282+github-actions[bot]@users.noreply.github.com", string user = "github-actions[bot]")
        {
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
                await $"git config --local user.email \"{email}\"";
                await $"git config --local user.name \"{user}\"";
                await $"git commit -a -m \"{EscapeArg($"feat: Update package.json to {tag}")}\" -m \"{EscapeArg($"Commit by [GitHub Actions]({GitHubContext.Current.WorkflowRunUrl})")}\"";

                commited = true;
            }

            var sha = await "git rev-parse HEAD";
            return (commited, sha, branchName, isBranchCreated);
        }

#pragma warning restore CA1822 // Mark members as static
    }

    internal class GlobalCompleteLogFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
    {
        public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
        {
            // don't show log on --help and --version
            var isMetaCommand = context.Arguments.Contains("--help") || context.Arguments.Contains("-h") || context.Arguments.Contains("--version");
            if (!isMetaCommand)
            {
                GitHubActions.WriteLog($"Begin {context.CommandName} ...");
            }
            await Next.InvokeAsync(context, cancellationToken);
            if (!isMetaCommand)
            {
                GitHubActions.WriteLog($"Completed ...");
            }
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
                // Ensure GitHubContext can be resolved
                _ = GitHubContext.Current;
            }
            await Next.InvokeAsync(context, cancellationToken);
        }
    }

    public class ActionCommandException(string message, Exception? innterException = null) : Exception(message, innterException);

    internal static class ActionsBatchOptions
    {
        public static readonly bool Verbose = bool.Parse(Environment.GetEnvironmentVariable("ACTIONS_BATCH_OPTIONS_VERBOSE") ?? "false");
    }
}