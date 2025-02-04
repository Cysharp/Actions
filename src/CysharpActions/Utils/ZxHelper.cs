namespace CysharpActions.Utils;

public static class ZxHelper
{
    /// <summary>
    /// **CAUTION**
    /// CONSIDER USE `Zx.Env.useShell = false` INSTEAD IF ESCAPE BY THIS METHOD.<br/>
    /// This method is needed only if you need run command with `Zx.Env.useShell = true`.<br/>
    /// 
    /// Escape command by adding \" on each side if needed. <br/>
    /// </summary>
    /// <remarks>
    /// ex. EscapeArgs("who") will be output as follows.<br/>
    /// * Windows: who <br/>
    /// * Linux/macOS: \"who\"
    /// </remarks>
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

    /// <summary>
    /// Split newline delimited string to string array.
    /// </summary>
    /// <param name="stringsValue"></param>
    /// <returns></returns>
    public static string[] ToMultiLine(this string stringsValue) => stringsValue.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
}
