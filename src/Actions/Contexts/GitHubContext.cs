using System.Text.Json;
using System.Text.Json.Serialization;

namespace Actions.Contexts;

public record GitHubContext
{
    public static readonly GitHubContext Current = JsonSerializer.Deserialize<GitHubContext>(Environment.GetEnvironmentVariable("GITHUB_CONTEXT") ?? "{}")!;

    [JsonPropertyName("server_url")]
    public required string ServerUrl { get; init; }
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }
    [JsonPropertyName("repository")]
    public required string Repository { get; init; }
    [JsonPropertyName("repository_owner")]
    public required string RepositoryOwner { get; init; }
    [JsonPropertyName("event_name")]
    public required string EventName { get; init; }

    public static void ThrowIfNotAvailable()
    {
        var env = Environment.GetEnvironmentVariable("GITHUB_CONTEXT") ?? throw new ArgumentNullException("Environment Variable 'GITHUB_CONTEXT' is missing.");
        ArgumentNullException.ThrowIfNullOrEmpty(Current.ServerUrl);
    }
    public static string GetWorkflowRunUrl(GitHubContext context) => $"{context.ServerUrl}/{context.Repository}/actions/runs/{context.RunId}";
}

