namespace CysharpActions.Tests;

public static class TestHelper
{
    public static void CreateFile(string path, string contents = "")
    {
        var tempDir = Path.GetDirectoryName(path) ?? throw new NullReferenceException(path);
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
        File.WriteAllText(path, contents);
    }

    public static void CreateFiles(string dir, string[] items, string contents = "", bool recursiveDir = false)
    {
        foreach (var item in items)
        {
            var tempDir = recursiveDir ? Path.Combine(dir, item) : dir;
            var file = Path.Combine(tempDir, item);
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            File.WriteAllText(file, contents);
        }
    }

    public static void SafeDeleteDirectory(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }
}

public static class StringExtentions
{
    public static string NormalizeEol(this string input)
    {
        return input.Replace("\r\n", "\n");
    }
}
