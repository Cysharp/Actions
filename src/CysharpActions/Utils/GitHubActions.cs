using System.Runtime.CompilerServices;

namespace CysharpActions.Utils;

public static class GitHubActions
{
    public static void WriteRawLog(string value) => Console.WriteLine(value);
    public static void WriteLog(string value) => Console.WriteLine($"[{DateTime.Now:s}] {value}");

    public static void WriteVerbose(string value)
    {
        if (ActionsBatchOptions.Verbose)
        {
            WriteLog(value);
        }
    }

    public static void SetOutput(string key, string value, [CallerMemberName] string? callerMemberName = null)
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

    /// <summary>
    /// Start GitHub Actions group sections. Dispose <see cref="GitHubActionsGroup"/> to close group
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    public static GitHubActionsGroup StartGroup(string title)
    {
        return new GitHubActionsGroup(title);
    }


    public class GitHubActionsGroup : IDisposable
    {
        public GitHubActionsGroup(string title)
        {
            Console.WriteLine($"::group::{title}");
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            Console.WriteLine("::endgroup::");
        }
    }
}
