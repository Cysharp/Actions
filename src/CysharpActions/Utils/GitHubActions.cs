using System.Runtime.CompilerServices;

namespace CysharpActions.Utils;

public static class GitHubActions
{
    private const string RedactedValue = "***";
    private static bool verbose;
    private static string? outputPath;

    public static void Configure(bool verbose, string? outputPath)
    {
        GitHubActions.verbose = verbose;
        GitHubActions.outputPath = outputPath;
    }

    public static void WriteRawLog(string value) => Console.WriteLine(value);

    public static void WriteRedactedRawLog(string value, params string?[] secrets)
    {
        foreach (var secret in secrets)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                value = value.Replace(secret, RedactedValue, StringComparison.Ordinal);
            }
        }

        WriteRawLog(value);
    }

    public static void WriteLog(string value) => Console.WriteLine($"[{DateTime.Now:s}] {value}");

    /// <summary>Writes an error annotation. Escaping keeps a newline in <paramref name="message"/> from starting another workflow command.</summary>
    public static void WriteError(string title, string message)
    {
        static string EscapeData(string value) => value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
        var escapedTitle = EscapeData(title).Replace(":", "%3A", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal);
        WriteRawLog($"::error title={escapedTitle}::{EscapeData(message)}");
    }

    public static void WriteVerbose(string value)
    {
        if (verbose)
        {
            WriteLog(value);
        }
    }

    public static void SetOutput(string key, string value, [CallerMemberName] string? callerMemberName = null)
    {
        var input = $"{key}={value}";
        var output = outputPath ?? Path.Combine(Directory.GetCurrentDirectory(), $"GitHubOutputs/{callerMemberName}");
        if (!Directory.Exists(Path.GetDirectoryName(output)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        }

        File.AppendAllLines(output, [input]);
        WriteLog($"GitHub Output: {input}");
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
