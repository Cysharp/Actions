using CysharpActions.Utils;
using Zx;

namespace CysharpActions.Tests;

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

        var dir = $".tests/{nameof(CreateReleaseCommand)}/{nameof(ReleaseTest)}";
        var file = $"{tag}.txt";
        var path = Path.Combine(dir, file);
        try
        {
            CreateFile(dir, file);
            var command = new CreateReleaseCommand(tag, releaseTitle);
            await command.CreateReleaseAsync();
            await command.UploadAssetFilesAsync([path]);
        }
        finally
        {
            SafeDeleteDirectory(dir);

            // clean up release
            var list = await $"gh release list";
            var exists = list.ToMultiLine()
                .Where(x => x.Contains("Draft"))
                .Where(x => x.Contains(releaseTitle))
                .Any();
            if (exists)
            {
                await $"gh release delete {tag} --yes --cleanup-tag";
            }
        }
    }
}
