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
    public static string EscapeArg(string command)
    {
        // Windows don't need escape (Windows escape is differ from Linux/macOS)
        if (OperatingSystem.IsWindows())
            return command;

        // Bash need escape
        return "\"" + command + "\"";
    }

    public static string[] ToMultiLine(this string stringsValue) => stringsValue.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
}
