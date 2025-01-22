namespace CysharpActions.Utils;

public static class ZxHelper
{
    /// <summary>
    /// Escape command by adding \" on each side if needed. <br/>
    /// ex. who will be... <br/>
    /// * Windows: who <br/>
    /// * Linux/macOS: \"who\"
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public static string EscapeArg(this string command)
    {
        // Windows don't need escape (Windows escape is differ from Linux/macOS)
        if (OperatingSystem.IsWindows())
            return command;

        // Bash need escape
        return "\"" + command + "\"";
    }

    /// <summary>
    /// Split string by new line.
    /// </summary>
    /// <param name="stringsValue"></param>
    /// <returns></returns>
    public static string[] SplitByNewLine(this string stringsValue) => stringsValue.Split(["\\r\\n", "\\n"], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Write log to console with timestamp.
    /// </summary>
    /// <param name="value"></param>
    public static void WriteLog(string value) => Console.WriteLine($"[{DateTime.Now:s}] {value}");

    /// <summary>
    /// Write log to console with timestamp if verbose is enabled.
    /// </summary>
    /// <param name="value"></param>
    public static void WriteVerbose(string value)
    {
        if (ActionsBatchOptions.Verbose)
        {
            WriteLog(value);
        }
    }
}
