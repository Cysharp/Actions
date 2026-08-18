using System.Collections;

namespace CysharpActions.Runtime;

public sealed record ActionEnvironment(
    bool CI,
    bool GitHubActions,
    bool Verbose,
    string? GitHubOutputPath,
    RepositoryContext Repository,
    WorkflowRunContext WorkflowRun,
    GitHubCredentials GitHubCredentials)
{
    public static ActionEnvironment ReadFromProcess()
    {
        var variables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry variable in variables)
        {
            if (variable.Key is string key)
                values[key] = variable.Value as string;
        }
        return Parse(values);
    }

    public static ActionEnvironment Parse(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var repository = Get(values, "GITHUB_REPOSITORY");
        var stepDebug = ReadBoolean(values, "ACTIONS_STEP_DEBUG");
        var runnerDebug = ReadZeroOrOne(values, "RUNNER_DEBUG");
        return new ActionEnvironment(
            ReadBoolean(values, "CI"),
            ReadBoolean(values, "GITHUB_ACTIONS"),
            stepDebug || runnerDebug,
            NullIfWhiteSpace(Get(values, "GITHUB_OUTPUT")),
            new RepositoryContext(repository),
            new WorkflowRunContext(
                Get(values, "GITHUB_SERVER_URL"),
                repository,
                Get(values, "GITHUB_RUN_ID")),
            new GitHubCredentials(
                NullIfWhiteSpace(Get(values, "GH_REPO")),
                NullIfWhiteSpace(Get(values, "GH_TOKEN"))));
    }

    public void ValidateGitHubCli()
    {
        if (CI)
            GitHubCredentials.Validate();
    }

    private static string Get(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value ?? "" : "";

    private static bool ReadBoolean(IReadOnlyDictionary<string, string?> values, string key)
    {
        var value = Get(values, key);
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (bool.TryParse(value, out var result))
            return result;
        throw InvalidValue(key, value, "true or false");
    }

    private static bool ReadZeroOrOne(IReadOnlyDictionary<string, string?> values, string key)
    {
        var value = Get(values, key);
        if (string.IsNullOrWhiteSpace(value) || value == "0")
            return false;
        if (value == "1")
            return true;
        throw InvalidValue(key, value, "0 or 1");
    }

    private static ActionCommandException InvalidValue(string key, string value, string expected) =>
        new($"Environment variable '{key}' must be {expected}, but was '{value}'.");

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

public readonly record struct RepositoryContext(string Repository)
{
    public string RequireRepository() => Required(Repository, "GITHUB_REPOSITORY");

    internal static string Required(string? value, string variableName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ActionCommandException($"Environment variable '{variableName}' is required.");
        return value;
    }
}

public readonly record struct WorkflowRunContext(string ServerUrl, string Repository, string RunId)
{
    public string WorkflowRunUrl =>
        $"{RepositoryContext.Required(ServerUrl, "GITHUB_SERVER_URL").TrimEnd('/')}/{RepositoryContext.Required(Repository, "GITHUB_REPOSITORY")}/actions/runs/{RepositoryContext.Required(RunId, "GITHUB_RUN_ID")}";
}

public sealed record GitHubCredentials(string? Repository, string? Token)
{
    public void Validate()
    {
        RepositoryContext.Required(Repository, "GH_REPO");
        RepositoryContext.Required(Token, "GH_TOKEN");
    }

    public (string Owner, string Name) ParseRepository()
    {
        var repository = RepositoryContext.Required(Repository, "GH_REPO");
        var separatorIndex = repository.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == repository.Length - 1 || separatorIndex != repository.LastIndexOf('/'))
            throw new ActionCommandException($"Environment variable 'GH_REPO' must use the 'owner/repository' format, but was '{repository}'.");
        return (repository[..separatorIndex], repository[(separatorIndex + 1)..]);
    }

    public string RequireToken() => RepositoryContext.Required(Token, "GH_TOKEN");

    public override string ToString() =>
        $"GitHubCredentials {{ Repository = {Repository}, Token = *** }}";
}
