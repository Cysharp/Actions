using CysharpActions.Contexts;

namespace CysharpActions.Tests;

public static class LiveGitHubTest
{
    public const string Category = "LiveGitHub";
    public const string SkipReason = "Requires GitHub Actions with GH_REPO and GH_TOKEN.";

    public static bool IsAvailable =>
        GitHubEnv.Current.CI &&
        !string.IsNullOrWhiteSpace(GHEnv.Current.GH_REPO) &&
        !string.IsNullOrWhiteSpace(GHEnv.Current.GH_TOKEN);
}

[CollectionDefinition(LiveGitHubTest.Category, DisableParallelization = true)]
public sealed class LiveGitHubCollection;
