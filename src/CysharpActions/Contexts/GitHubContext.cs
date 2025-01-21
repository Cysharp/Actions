using System.Text.Json;
using System.Text.Json.Serialization;

namespace CysharpActions.Contexts;

public record GitHubContext
{
    public static readonly GitHubContext Current = JsonSerializer.Deserialize<GitHubContext>(ActionsBatchOptions.GitHubContext ?? "{}")!;

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
    public string WorkflowRunUrl => $"{ServerUrl}/{Repository}/actions/runs/{RunId}";

    public static void ThrowIfNotAvailable()
    {
        // This should be throw when Environment Variable is missing.
        _ = ActionsBatchOptions.GitHubContext ?? throw new ArgumentNullException("Environment Variable 'GITHUB_CONTEXT' is missing.");
        // This should be throw when required property is missing.
        _ = GitHubContext.Current;
    }
}

