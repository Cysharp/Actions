using CysharpActions.Runtime;

namespace CysharpActions.Tests;

public class ActionEnvironmentTest
{
    [Fact]
    public void ParseCreatesImmutableCommandViewsTest()
    {
        var values = new Dictionary<string, string?>
        {
            ["CI"] = "true",
            ["GITHUB_ACTIONS"] = "true",
            ["ACTIONS_STEP_DEBUG"] = "false",
            ["RUNNER_DEBUG"] = "1",
            ["GITHUB_OUTPUT"] = "/tmp/github-output",
            ["GITHUB_REPOSITORY"] = "owner/repository",
            ["GITHUB_SERVER_URL"] = "https://github.example/",
            ["GITHUB_RUN_ID"] = "123",
            ["GH_REPO"] = "api-owner/api-repository",
            ["GH_TOKEN"] = "secret-token",
        };

        var environment = ActionEnvironment.Parse(values);
        values["GITHUB_REPOSITORY"] = "changed/after-parse";
        values["GH_TOKEN"] = "changed-token";

        Assert.True(environment.CI);
        Assert.True(environment.GitHubActions);
        Assert.True(environment.Verbose);
        Assert.Equal("/tmp/github-output", environment.GitHubOutputPath);
        Assert.Equal("owner/repository", environment.Repository.Repository);
        Assert.Equal("https://github.example/owner/repository/actions/runs/123", environment.WorkflowRun.WorkflowRunUrl);
        Assert.Equal(("api-owner", "api-repository"), environment.GitHubCredentials.ParseRepository());
        Assert.Equal("secret-token", environment.GitHubCredentials.RequireToken());
        Assert.DoesNotContain("secret-token", environment.GitHubCredentials.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseUsesLocalDefaultsWhenVariablesAreMissingTest()
    {
        var environment = ActionEnvironment.Parse(new Dictionary<string, string?>());

        Assert.False(environment.CI);
        Assert.False(environment.GitHubActions);
        Assert.False(environment.Verbose);
        Assert.Null(environment.GitHubOutputPath);
        Assert.Equal(string.Empty, environment.Repository.Repository);
        Assert.Null(environment.GitHubCredentials.Repository);
        Assert.Null(environment.GitHubCredentials.Token);
    }

    [Theory]
    [InlineData("CI", "1")]
    [InlineData("GITHUB_ACTIONS", "yes")]
    [InlineData("ACTIONS_STEP_DEBUG", "enabled")]
    [InlineData("RUNNER_DEBUG", "true")]
    [InlineData("RUNNER_DEBUG", "2")]
    public void ParseRejectsInvalidBooleanWithVariableNameTest(string variableName, string value)
    {
        var values = new Dictionary<string, string?> { [variableName] = value };

        var exception = Assert.Throws<ActionCommandException>(() => ActionEnvironment.Parse(values));

        Assert.Contains(variableName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(value, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "token", "GH_REPO")]
    [InlineData("owner/repository", null, "GH_TOKEN")]
    public void CredentialsValidationNamesMissingVariableTest(string? repository, string? token, string variableName)
    {
        var credentials = new GitHubCredentials(repository, token);

        var exception = Assert.Throws<ActionCommandException>(credentials.Validate);

        Assert.Contains(variableName, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("repository")]
    [InlineData("/repository")]
    [InlineData("owner/")]
    [InlineData("owner/repository/extra")]
    public void CredentialsRejectInvalidRepositoryFormatTest(string repository)
    {
        var credentials = new GitHubCredentials(repository, "token");

        var exception = Assert.Throws<ActionCommandException>(() => credentials.ParseRepository());

        Assert.Contains("GH_REPO", exception.Message, StringComparison.Ordinal);
        Assert.Contains("owner/repository", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitCommandRejectsMissingRepositoryBeforeRunningProcessTest()
    {
        var processCalled = false;
        Task<ProcessResult> Run(CommandSpec command, CancellationToken cancellationToken)
        {
            processCalled = true;
            return Task.FromResult(new ProcessResult(0, "", ""));
        }

        var command = new GitCommand(runProcess: Run);
        var exception = await Assert.ThrowsAsync<ActionCommandException>(() =>
            command.DeleteBranchAsync("branch", new RepositoryContext(""), TestContext.Current.CancellationToken));

        Assert.Contains("GITHUB_REPOSITORY", exception.Message, StringComparison.Ordinal);
        Assert.False(processCalled);
    }
}
