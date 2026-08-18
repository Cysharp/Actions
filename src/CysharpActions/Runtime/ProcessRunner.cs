using Cysharp.Diagnostics;
using CysharpActions.Utils;
using System.Diagnostics;
using System.Text;

namespace CysharpActions.Runtime;

public readonly record struct CommandSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlySet<int>? SecretArguments = null)
{
    public override string ToString() => ToDisplayString();

    public string ToDisplayString()
    {
        Validate();

        var builder = new StringBuilder(Quote(FileName));
        for (var i = 0; i < Arguments.Count; i++)
        {
            builder.Append(' ');
            builder.Append(SecretArguments?.Contains(i) == true ? "***" : Quote(Arguments[i]));
        }
        return builder.ToString();
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FileName);
        ArgumentNullException.ThrowIfNull(Arguments);
        var argumentCount = Arguments.Count;
        if (SecretArguments?.Any(index => index < 0 || index >= argumentCount) == true)
            throw new InvalidOperationException("Secret argument index is outside the argument list.");
    }

    private static string Quote(string value)
    {
        if (value.Length != 0 && !value.Any(char.IsWhiteSpace) && !value.Contains('"'))
            return value;

        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}

public readonly record struct ProcessResult(int ExitCode, string Stdout, string Stderr)
{
    public string[] OutputLines => Stdout.ToMultiLine();
}

public delegate Task<ProcessResult> RunProcess(CommandSpec command, CancellationToken cancellationToken = default);

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
    {
        command.Validate();

        var startInfo = new ProcessStartInfo(command.FileName)
        {
            UseShellExecute = false,
        };
        if (!string.IsNullOrWhiteSpace(Env.workingDirectory))
        {
            startInfo.WorkingDirectory = Env.workingDirectory;
        }
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var lines = await ProcessX.StartAsync(startInfo).ToTask(cancellationToken);
        return new ProcessResult(0, string.Join(Environment.NewLine, lines), "");
    }

    public static void WritePreview(CommandSpec command) =>
        GitHubActions.WriteRawLog(command.ToDisplayString());
}
