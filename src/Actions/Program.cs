using Actions;
using Actions.Commands;
using ConsoleAppFramework;

namespace Actions;

public class Program
{
    private static void Main(string[] args)
    {
        var app = ConsoleApp.Create();
        app.Add<ActionsBatch>();
        app.Run(args);
    }
}

public class ActionsBatch
{
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

        Console.WriteLine(OutputFormat("version", versioning, outputFormat));
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
        if (string.IsNullOrWhiteSpace(path))
            Console.WriteLine("Input is empty path, skip execution.");

        var command = new UpdateVersionCommand(version, path);
        var (_, after) = command.UpdateVersion(dryRun);

        if (dryRun)
        {
            Console.WriteLine(after);
        }
    }

    /// <summary>
    /// Create dummy files
    /// </summary>
    /// <param name="basePath"></param>
    [Command("create-dummy")]
    public void CreateDummy(string basePath)
    {
        var command = new CreateDummyCommand();
        command.CreateDummyFiles(basePath);
    }

    private static string OutputFormat(string key, string value, OutputFormatType format) => format switch
    {
        OutputFormatType.Console => value,
        OutputFormatType.GitHubActions => $"{key}={value}",
        _ => throw new NotImplementedException(nameof(format)),
    };
}

public class ActionCommandException(string message) : Exception(message)
{
}
