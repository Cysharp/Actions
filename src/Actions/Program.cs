using Actions;
using Actions.Commands;
using Actions.Utils;
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

        var output = OutputFormat("version", versioning, outputFormat);
        Console.WriteLine(output);
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
        Console.WriteLine($"Update begin, {path} ...");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Empty path detected, skip execution.");
            return;
        }

        using (var githubGroup = new GitHubActionsGroupLogger("Before"))
            Console.WriteLine(File.ReadAllText(path));

        var command = new UpdateVersionCommand(version, path);
        var result = command.UpdateVersion(dryRun);

        using (var githubGroup = new GitHubActionsGroupLogger("After"))
            Console.WriteLine(result.After);

        Console.WriteLine($"Completed ...");
    }

    /// <summary>
    /// Create dummy files
    /// </summary>
    /// <param name="basePath"></param>
    [Command("create-dummy")]
    public void CreateDummy(string basePath)
    {
        Console.WriteLine($"Creating dummy files, under {basePath} ...");

        var command = new CreateDummyCommand();
        command.CreateDummyFiles(basePath);

        Console.WriteLine($"Completed ...");
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
