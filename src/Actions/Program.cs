using Actions;
using Actions.Handlers;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<ActionsCommand>();
app.Run(args);

public class ActionsCommand
{
    /// <summary>Get version string from tag</summary>
    /// <param name="tag"></param>
    /// <param name="prefix"></param>
    /// <param name="versionIncrement"></param>
    /// <param name="isPrelease"></param>
    /// <param name="prerelease"></param>
    /// <param name="outputFormat"></param>
    [Command("versioning")]
    public void Versioning(string tag, string prefix = "", VersionIncrement versionIncrement = VersionIncrement.Patch, bool isPrelease = false, string prerelease = "preview", OutputFormatType outputFormat = OutputFormatType.Console)
    {
        var handler = new VersionHandler(tag, prefix, versionIncrement, isPrelease, prerelease);
        var versioning = handler.Versioning();

        Console.WriteLine(OutputFormat("version", versioning, outputFormat));
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
