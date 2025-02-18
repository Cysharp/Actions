using System.Text.RegularExpressions;

namespace CysharpActions.Utils;

/// <summary>
/// Similar to GNU sed
/// </summary>
public static class RegrexReplace
{
    /// <summary>
    /// Replace file contents with pattern and replacement.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="pattern"></param>
    /// <param name="replacement"></param>
    /// <param name="writeBack">ファイルに書き込むかどうか。falseの場合ファイルに書き込みません。</param>
    /// <returns></returns>
    public static (string before, string after) Replace(string path, string pattern, string replacement, bool writeBack)
    {
        var input = File.ReadAllText(path);
        var result = Replace(input, pattern, replacement);

        if (writeBack)
        {
            File.WriteAllText(path, result.after);
        }

        return result;
    }

    /// <summary>
    /// Replace contents with pattern and replacement.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="pattern"></param>
    /// <param name="replacement"></param>
    /// <returns></returns>
    public static (string before, string after) Replace(string input, string pattern, string replacement)
    {
        var updatedContent = Regex.Replace(input, pattern, replacement, RegexOptions.Multiline);
        return (input, updatedContent);
    }
}
