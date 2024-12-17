using Actions;
using Actions.Commands;
using Actions.Utils;
using ConsoleAppFramework;

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
        /// Update Version for specified path
        /// </summary>
        /// <param name="version"></param>
        /// <param name="path"></param>
        /// <param name="dryRun"></param>
        [Command("update-version")]
        public void UpdateVersion(string version, string path, bool dryRun)
        {
            WriteLog($"Update begin, {path} ...");
            if (string.IsNullOrWhiteSpace(path))
            {
                WriteLog("Empty path detected, skip execution.");
                return;
            }

            using (var githubGroup = new GitHubActionsGroupLogger("Before"))
                WriteLog(File.ReadAllText(path));

            var command = new UpdateVersionCommand(version, path);
            var result = command.UpdateVersion(dryRun);

            using (var githubGroup = new GitHubActionsGroupLogger("After"))
                WriteLog(result.After);

            WriteLog($"Completed ...");
        }

        /// <summary>
        /// Validate specified path contains file
        /// </summary>
        /// <param name="pathPattern"></param>
        /// <param name="verbose"></param>
        [Command("validate-file-exists")]
        public void ValidateFileExists(string pathPattern, bool verbose)
        {
            _verbose = verbose;
            WriteLog($"Validating path, {pathPattern} ...");
            WriteVerbose($"UTF8: {DebugTools.ToUtf8Base64String(pathPattern)}");
            var command = new FileExsistsCommand(pathPattern);
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

#pragma warning restore CA1822 // Mark members as static

        private static string OutputFormat(string key, string value, OutputFormatType format) => format switch
        {
            OutputFormatType.Console => value,
            OutputFormatType.GitHubActions => $"{key}={value}",
            _ => throw new NotImplementedException(nameof(format)),
        };

        void WriteLog(string value)
        {
            Console.WriteLine($"[{DateTime.Now:s}] {value}");
        }

        void WriteVerbose(string value)
        {
            if (_verbose)
            {
                Console.WriteLine($"[{DateTime.Now:s}] {value}");
            }
        }
    }

    public class ActionCommandException(string message, Exception? innterException = null) : Exception(message, innterException)
    {
    }
}