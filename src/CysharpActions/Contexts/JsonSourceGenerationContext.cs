using System.Text.Json.Serialization;

namespace CysharpActions.Contexts;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GitHubApiRepo))]
[JsonSerializable(typeof(GitHubApiBranches[]))]
[JsonSerializable(typeof(GitHubApiBranch))]
[JsonSerializable(typeof(UpmPackageJson))]
[JsonSerializable(typeof(GitHubRelease[]))] 
internal partial class JsonSourceGenerationContext : JsonSerializerContext
{
}
internal record GitHubApiRepo
{
    [JsonPropertyName("default_branch")]
    public required string DefaultBranch { get; init; }
}

internal record GitHubApiBranches
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

internal record GitHubApiBranch
{
    [JsonPropertyName("commit")]
    public required GitHubApiBranchCommit Commit { get; init; }

    internal record GitHubApiBranchCommit
    {
        [JsonPropertyName("author")]
        public required GitHubApiBranchAuthor Author { get; init; }
    }

    internal record GitHubApiBranchAuthor
    {
        [JsonPropertyName("login")]
        public required string Login { get; init; }
    }
}

internal record UpmPackageJson
{
    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

public record GitHubRelease
{
    [JsonPropertyName("tagName")]
    public required string TagName { get; init; }
    [JsonPropertyName("isLatest")]
    public required bool IsLatest { get; init; }
}
