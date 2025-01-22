namespace CysharpActions.Contexts;

/// <summary>
/// GitHub Context to access. Context is resolved by GitHub Environment Variables.
/// </summary>
public record GitHubContext
{
    public static GitHubContext Current { get; } = new GitHubContext
    {
        EventName = GitHubEnvironmentVariables.Current.GitHubEventName,
        GitHubActions = GitHubEnvironmentVariables.Current.GitHubActions,
        Repository = GitHubEnvironmentVariables.Current.GitHubRepository,
        RunId = GitHubEnvironmentVariables.Current.RunId,
        ServerUrl = GitHubEnvironmentVariables.Current.GitHubServerUrl
    };

    /// <summary>
    /// The name of the event that triggered the workflow. For example, workflow_dispatch.
    /// </summary>
    /// <remarks>
    /// push, pull_request...
    /// </remarks>
    public required string EventName { get; init; }
    /// <summary>
    /// Always set to true when GitHub Actions is running the workflow. You can use this variable to differentiate when tests are being run locally or by GitHub Actions.
    /// </summary>
    /// <remarks>
    /// true when running on GitHub Actions
    /// </remarks>
    public required bool GitHubActions { get; init; }
    /// <summary>
    /// The owner and repository name. For example, octocat/Hello-World.
    /// </summary>
    /// <remarks>
    /// Cysharp/Actions
    /// </remarks>
    public required string Repository { get; init; }
    /// <summary>
    /// A unique number for each workflow run within a repository. This number does not change if you re-run the workflow run. For example, 1658821493.
    /// </summary>
    /// <remarks>
    /// 11350100787
    /// </remarks>
    public required string RunId { get; init; }
    /// <summary>
    /// The URL of the GitHub server. For example: https://github.com.
    /// </summary>
    /// <remarks>
    /// https://github.com
    /// </remarks>
    public required string ServerUrl { get; init; }
    /// <summary>
    /// URL to this workflow run
    /// </summary>
    /// <remarks>
    /// https://github.com/Cysharp/Actions/runs/11350100787
    /// </remarks>
    public string WorkflowRunUrl => $"{ServerUrl}/{Repository}/actions/runs/{RunId}";
}

/// <summary>
/// see: https://docs.github.com/en/actions/writing-workflows/choosing-what-your-workflow-does/store-information-in-variables#default-environment-variables
/// </summary>
internal record GitHubEnvironmentVariables
{
    public static GitHubEnvironmentVariables Current { get; } = new GitHubEnvironmentVariables();

    /// <summary>
    /// Always set to true when GitHub Actions is running the workflow. You can use this variable to differentiate when tests are being run locally or by GitHub Actions.
    /// </summary>
    public bool GitHubActions { get; init; } = bool.Parse(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") ?? "false");
    /// <summary>
    /// The name of the base ref or target branch of the pull request in a workflow run. This is only set when the event that triggers a workflow run is either pull_request or pull_request_target. For example, main.
    /// </summary>
    public string GitHubBaseRef { get; init; } = Environment.GetEnvironmentVariable("GITHUB_BASE_REF") ?? "";
    /// <summary>
    /// The name of the event that triggered the workflow. For example, workflow_dispatch.
    /// </summary>
    public string GitHubEventName { get; init; } = Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") ?? throw new ArgumentNullException("Environment Variable 'GITHUB_EVENT_NAME' is missing.");
    /// <summary>
    /// The owner and repository name. For example, octocat/Hello-World.
    /// </summary>
    public string GitHubRepository { get; init; } = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? throw new ArgumentNullException("Environment Variable 'GITHUB_REPOSITORY' is missing.");
    /// <summary>
    /// The URL of the GitHub server. For example: https://github.com.
    /// </summary>
    public string GitHubServerUrl { get; init; } = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL") ?? throw new ArgumentNullException("Environment Variable 'GITHUB_SERVER_URL' is missing.");
    /// <summary>
    /// The commit SHA that triggered the workflow. The value of this commit SHA depends on the event that triggered the workflow. For more information, see Events that trigger workflows. For example, ffac537e6cbbf934b08745a378932722df287a53.
    /// </summary>
    public string GitHubSha { get; init; } = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "";
    /// <summary>
    /// The username of the user that initiated the workflow run. If the workflow run is a re-run, this value may differ from github.actor. Any workflow re-runs will use the privileges of github.actor, even if the actor initiating the re-run (github.triggering_actor) has different privileges.
    /// </summary>
    public string GitHubTriggerActor { get; init; } = Environment.GetEnvironmentVariable("GITHUB_TRIGGERING_ACTOR") ?? "";
    /// <summary>
    /// The name of the workflow. For example, My test workflow. If the workflow file doesn't specify a name, the value of this variable is the full path of the workflow file in the repository.
    /// </summary>
    public string GitHubWorkflow { get; init; } = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? "";
    /// <summary>
    /// The default working directory on the runner for steps, and the default location of your repository when using the checkout action. For example, /home/runner/work/my-repo-name/my-repo-name.
    /// </summary>
    public string GitHubWorkSpace { get; init; } = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? "";
    /// <summary>
    /// The head ref or source branch of the pull request in a workflow run. This property is only set when the event that triggers a workflow run is either pull_request or pull_request_target. For example, feature-branch-1.
    /// </summary>
    public string HeadRef { get; init; } = Environment.GetEnvironmentVariable("HEAD_REF") ?? "";
    /// <summary>
    /// A unique number for each workflow run within a repository. This number does not change if you re-run the workflow run. For example, 1658821493.
    /// </summary>
    public string RunId { get; init; } = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? throw new ArgumentNullException("Environment Variable 'GITHUB_RUN_ID' is missing.");
    /// <summary>
    /// A unique number for each run of a particular workflow in a repository. This number begins at 1 for the workflow's first run, and increments with each new run. This number does not change if you re-run the workflow run. For example, 3.
    /// </summary>
    public string RunNumber { get; init; } = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "";
    /// <summary>
    /// The architecture of the runner executing the job. Possible values are X86, X64, ARM, or ARM64.
    /// </summary>
    public string RunnerArch { get; init; } = Environment.GetEnvironmentVariable("RUNNER_ARCH") ?? "";
    /// <summary>
    /// The operating system of the runner executing the job. Possible values are Linux, Windows, or macOS. For example, Windows
    /// </summary>
    public string RunnerOS { get; init; } = Environment.GetEnvironmentVariable("RUNNER_OS") ?? "";
}
