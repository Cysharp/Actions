using CysharpActions.Contexts;

using CysharpActions.Runtime;

namespace CysharpActions.Tests;

public static class LiveGitHubTest
{
    public const string Category = "LiveGitHub";
    public const string SkipReason = "Requires GitHub Actions with GH_REPO and GH_TOKEN.";

    public static bool IsAvailable
    {
        get
        {
            var environment = ActionEnvironment.ReadFromProcess();
            return environment.CI &&
                   !string.IsNullOrWhiteSpace(environment.GitHubCredentials.Repository) &&
                   !string.IsNullOrWhiteSpace(environment.GitHubCredentials.Token);
        }
    }
}

[CollectionDefinition(LiveGitHubTest.Category, DisableParallelization = true)]
public sealed class LiveGitHubCollection;
