using System.Text;

namespace Actions.Utils;

public static class DebugTools
{
    public static string ToUtf8Base64String(string input) => string.Join(" ", Encoding.UTF8.GetBytes(input).Select(b => b.ToString("X2")));
}
