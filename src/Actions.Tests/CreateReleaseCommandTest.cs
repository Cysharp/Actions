using Actions.Commands;
using Zx;

namespace Actions.Tests;

public class CreateReleaseCommandTest
{
    [Theory]
    [InlineData("test.0.1.0", "Ver.test.0.1.0")]
    [InlineData("test.1.0.0", "Ver.test.1.0.0")]
    [InlineData("test.10.1.0", "Ver.test.10.1.0")]
    public async Task ReleaseTest(string tag, string releaseTitle)
    {
        if (Environment.GetEnvironmentVariable("CI") is null)
            return;
        if (Environment.GetEnvironmentVariable("GH_REPO") is null)
            throw new Exception("GH_REPO is not set");
        if (Environment.GetEnvironmentVariable("GH_TOKEN") is null)
            throw new Exception("GH_TOKEN is not set");

        var dir = Path.Combine(Path.GetTempPath(), nameof(ReleaseTest));
        var files = Enumerable.Range(0, 3)
            .Select(x => Path.Combine(dir, Path.GetTempFileName()))
            .ToArray();
        try
        {
            CreateFiles(dir, files);
            var command = new CreateReleaseCommand(tag, releaseTitle);
            await command.CreateReleaseAsync();
            await command.UploadAssetFiles(files);
        }
        finally
        {
            SafeDeleteDir(dir);

            // clean up release
            var list = await $"gh release list";
            var exists = SplitByNewLine(list)
                .Where(x => x.Contains("Draft"))
                .Where(x => x.Contains("Ver.1.1.0"))
                .Any();
            if (exists)
            {
                await $"gh release delete {tag} --yes --cleanup-tag";
            }
        }
    }

    private static string[] SplitByNewLine(string stringsValue) => stringsValue.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

    private void CreateFiles(string dir, string[] files)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            foreach (var file in files)
            {
                File.WriteAllText(file, "");
            }
        }
    }

    private void SafeDeleteDir(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir);
    }
}
