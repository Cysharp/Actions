﻿using Actions;
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

        private bool _verbose;

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

            foreach (var path in paths)
            {

                WriteLog($"Update begin, {path} ...");
                if (string.IsNullOrWhiteSpace(path))
                {
                    WriteLog("Empty path detected, skip execution.");
                    continue;
                }

                // Update Version
                using (var githubGroup = new GitHubActionsGroupLogger("Before"))
                    WriteLog(File.ReadAllText(path));
                var command = new UpdateVersionCommand(version, path);
                var result = command.UpdateVersion(dryRun);
                using (var githubGroup = new GitHubActionsGroupLogger("After"))
                    WriteLog(result.After);
            }

            // Git Commit
            using (var githubGroup = new GitHubActionsGroupLogger("git commit changes"))
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
        /// <param name="verbose"></param>
        [Command("validate-file-exists")]
        public void ValidateFileExists(string pathPattern, bool verbose)
        {
            SetOptions(verbose);

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
        /// <param name="verbose"></param>
        [Command("validate-nupkg-exists")]
        public void ValidateNupkgExists(string pathPattern, bool verbose)
        {
            SetOptions(verbose);
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
            try
            {
                var result = await "git diff --exit-code";
                WriteLog("There is no git diff.");
                GitHubOutput("commited", "0");
                GitHubOutput("sha", "");
                GitHubOutput("is-branch-created", dryRun.ToString().ToLower());
            }
            catch (ProcessErrorException)
            {
                WriteLog("Detected git diff.");
                var commitMessageTitle = $"feat: Update package.json to {tag}";
                var commitMessageBody = $"Commit by [GitHub Actions]({GitHubContext.GetWorkflowRunUrl(GitHubContext.Current)})";
                await $"git config --local user.email \"{email}\"";
                await $"git config --local user.name \"{user}\"";
                await $"git commit -a -m \"{commitMessageTitle}\" -m \"{commitMessageBody}\"";

                var sha = await "git rev-parse HEAD";
                GitHubOutput("commited", "1");
                GitHubOutput("sha", sha);
                GitHubOutput("is-branch-created", dryRun.ToString().ToLower());
            }
        }


#pragma warning restore CA1822 // Mark members as static

        private static string OutputFormat(string key, string value, OutputFormatType format) => format switch
        {
            OutputFormatType.Console => value,
            OutputFormatType.GitHubActions => $"{key}={value}",
            _ => throw new NotImplementedException(nameof(format)),
        };

        private void SetOptions(bool verbose)
        {
            _verbose = verbose;
        }

        private void WriteLog(string value)
        {
            Console.WriteLine($"[{DateTime.Now:s}] {value}");
        }

        private void WriteVerbose(string value)
        {
            if (_verbose)
            {
                Console.WriteLine($"[{DateTime.Now:s}] {value}");
            }
        }

        private static void GitHubOutput(string key, string value, [CallerMemberName]string? callerMemberName = null)
        {
            var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT", EnvironmentVariableTarget.Process) ?? Path.Combine(Directory.GetCurrentDirectory(), $"GitHubOutputs/{callerMemberName}");
            if (!Directory.Exists(Path.GetDirectoryName(output)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            }
            File.AppendAllLines(output, [$"{key}={value}"]);
        }
    }

    public class ActionCommandException(string message, Exception? innterException = null) : Exception(message, innterException)
    {
    }
}