using CysharpActions.Contexts;
using CysharpActions.Utils;
using Zx;

using CysharpActions.Runtime;

namespace CysharpActions.Tests;

[Collection(LiveGitHubTest.Category)]
[Trait("Category", LiveGitHubTest.Category)]
public class CreateReleaseCommandTest
{
    [Theory(
        Skip = LiveGitHubTest.SkipReason,
        SkipUnless = nameof(LiveGitHubTest.IsAvailable),
        SkipType = typeof(LiveGitHubTest))]
    [InlineData("1.2.0-pre", "v1.2.0-pre")]
    public async Task SkipTagAndReleaseTest(string tag, string releaseTitle)
    {
        var environment = ActionEnvironment.ReadFromProcess();
        environment.GitHubCredentials.Validate();

        Zx.Env.useShell = false;

        var dir = $".tests/{nameof(CreateReleaseCommand)}/{nameof(CreateTagAndReleaseTest)}";
        var file = $"{tag}.txt";
        var path = Path.Combine(dir, file);
        try
        {
            CreateFile(path, tag);
            var command = new CreateReleaseCommand(tag, releaseTitle);
            await command.CreateReleaseAsync(environment.GitHubCredentials, TestContext.Current.CancellationToken);
            await command.UploadAssetFilesAsync([path], TestContext.Current.CancellationToken);
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
                // leave tag as is
                await $"gh release delete {tag} --yes";
            }
        }
    }

    [Theory(
        Skip = LiveGitHubTest.SkipReason,
        SkipUnless = nameof(LiveGitHubTest.IsAvailable),
        SkipType = typeof(LiveGitHubTest))]
    [InlineData("test.0.1.0", "Ver.test.0.1.0")]
    [InlineData("test.1.0.0", "Ver.test.1.0.0")]
    [InlineData("test.10.1.0", "Ver.test.10.1.0")]
    public async Task CreateTagAndReleaseTest(string tag, string releaseTitle)
    {
        var environment = ActionEnvironment.ReadFromProcess();
        environment.GitHubCredentials.Validate();

        Zx.Env.useShell = false;

        var dir = $".tests/{nameof(CreateReleaseCommand)}/{nameof(CreateTagAndReleaseTest)}";
        var file = $"{tag}.txt";
        var path = Path.Combine(dir, file);
        try
        {
            CreateFile(path, tag);
            var command = new CreateReleaseCommand(tag, releaseTitle);
            await command.CreateReleaseAsync(environment.GitHubCredentials, TestContext.Current.CancellationToken);
            await command.UploadAssetFilesAsync([path], TestContext.Current.CancellationToken);
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
