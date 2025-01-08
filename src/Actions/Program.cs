using Actions;
using Actions.Commands;
using Actions.Contexts;
using Actions.Utils;
using Cysharp.Diagnostics;
using System.Runtime.CompilerServices;

var app = ConsoleApp.Create();
app.Add<ActionsBatch>();
app.Run(args);

namespace Actions
{
    public class ActionsBatch
    {
#pragma warning disable CA1822 // Mark members as static

        /// <summary>Get version string from tag</summary>
        /// <param name="tag"></param>
        /// <param name="prefix"></param>
        /// <param name="versionIncrement"></param>
        /// <param name="isPrelease"></param>
        /// <param name="prerelease"></param>
        /// <param name="outputFormat"></param>
        [Command("versioning")]
        public void Versioning(string tag, string prefix = "", VersionIncrement versionIncrement = VersionIncrement.Patch, bool isPrelease = false, string prerelease = "preview", bool withoutPrefix = false, OutputFormatType outputFormat = OutputFormatType.Console)
        {
            var command = new VersioningCommand(tag, prefix, versionIncrement, isPrelease, prerelease);
            var versioning = command.Versioning(withoutPrefix);

            var output = OutputFormat("version", versioning, outputFormat);
            WriteLog(output);
        }

        /// <summary>
        /// Update Version for specified path and commit
        /// </summary>
        /// <param name="version"></param>
        /// <param name="paths"></param>
        /// <param name="dryRun"></param>
        [Command("update-version")]
        public async Task<int> UpdateVersion(string version, string[] paths, bool dryRun)
        {
            GitHubContext.ThrowIfNotAvailable();

            // update version
            var command = new UpdateVersionCommand(version);
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
        /// <param name="pathPattern"></param>
        [Command("validate-file-exists")]
        public void ValidateFileExists(string pathPattern)
        {
            WriteLog($"Validating path, {pathPattern} ...");
            WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(pathPattern)}");
            if (string.IsNullOrWhiteSpace(pathPattern))
            {
                WriteLog("Empty path detected, skip execution.");
                return;
            }

            var command = new FileExsistsCommand(pathPattern);
            command.Validate();

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Validate specified path contains nuget packages
        /// </summary>
        /// <param name="pathPattern"></param>
        [Command("validate-nupkg-exists")]
        public void ValidateNupkgExists(string pathPattern)
        {
            var fileName = Path.GetFileName(pathPattern);
            var allowMissing = Path.GetExtension(fileName) == ".snupkg";

            WriteLog($"Validating path, {pathPattern} ...");
            WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(pathPattern)}");
            if (string.IsNullOrWhiteSpace(pathPattern))
            {
                WriteLog("Empty path detected, skip execution.");
                return;
            }
            if (allowMissing)
            {
                WriteLog(".snupkg detected, allow missing file.");
            }

            var command = new FileExsistsCommand(pathPattern, allowMissing);
            command.Validate();

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

        /// <summary>
        /// Escape command by adding \" on each side if needed. <br/>
        /// ex. who will be... <br/>
        /// * Windows: who <br/>
        /// * Linux/macOS: \"who\"
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private static string EscapeArg(string command)
        {
            // Windows don't need escape (Windows escape is differ from Linux/macOS)
            if (OperatingSystem.IsWindows())
                return command;

            // Bash need escape
            return "\"" + command + "\"";
        }

        private static string OutputFormat(string key, string value, OutputFormatType format) => format switch
        {
            OutputFormatType.Console => value,
            OutputFormatType.GitHubActions => $"{key}={value}",
            _ => throw new NotImplementedException(nameof(format)),
        };

        private static void WriteLog(string value)
        {
            Console.WriteLine($"[{DateTime.Now:s}] {value}");
        }

        private static void WriteVerbose(string value)
        {
            if (ActionsBatchOptions.Verbose)
            {
                WriteLog(value);
            }
        }

        private static void GitHubOutput(string key, string value, [CallerMemberName]string? callerMemberName = null)
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

    public class ActionCommandException(string message, Exception? innterException = null) : Exception(message, innterException);

    internal static class ActionsBatchOptions
    {
        public static readonly bool Verbose = bool.Parse(Environment.GetEnvironmentVariable("ACTIONS_BATCH_OPTIONS_VERBOSE") ?? "false");
        public static readonly string? GitHubContext = Environment.GetEnvironmentVariable("GITHUB_CONTEXT");
    }
}