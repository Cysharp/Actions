using CysharpActions.Runtime;
using System.Text.Json;

namespace CysharpActions.Tests;

[Collection(LiveGitHubTest.Category)]
[Trait("Category", LiveGitHubTest.Category)]
public class ReleaseLifecycleLiveTest
{
    [Fact(
        Skip = LiveGitHubTest.SkipReason,
        SkipUnless = nameof(LiveGitHubTest.IsAvailable),
        SkipType = typeof(LiveGitHubTest))]
    public async Task CreatesAndCleansUpOnlyItsOwnDraftAndTag()
    {
        var environment = ActionEnvironment.ReadFromProcess();
        var tag = $"0.0.0-ci-{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), tag + ".json");
        var asset = Path.Combine(Path.GetTempPath(), tag + ".txt");
        var lifecycle = new ReleaseLifecycleCommand();
        try
        {
            await lifecycle.CreateAsync(tag, tag, path, environment.GitHubCredentials, TestContext.Current.CancellationToken);
            var created = JsonSerializer.Deserialize<ReleaseState>(File.ReadAllText(path))!;
            Assert.True(created.CreatedTag);
            Assert.NotNull(created.ReleaseId);
            File.WriteAllText(asset, "Release lifecycle smoke test.");
            await new CreateReleaseCommand(tag, tag).UploadAssetFilesAsync([asset], TestContext.Current.CancellationToken);
            await lifecycle.CleanupAsync(path, environment.GitHubCredentials, TestContext.Current.CancellationToken);
            var cleaned = JsonSerializer.Deserialize<ReleaseState>(File.ReadAllText(path))!;
            Assert.False(cleaned.CreatedTag);
            Assert.Null(cleaned.ReleaseId);
        }
        finally
        {
            File.Delete(asset);
            if (File.Exists(path))
            {
                // Preserve the local journal if cleanup fails, rather than guessing ownership.
                await lifecycle.CleanupAsync(path, environment.GitHubCredentials, CancellationToken.None);
                File.Delete(path);
            }
        }
    }
}
