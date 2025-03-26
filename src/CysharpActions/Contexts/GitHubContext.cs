using System.Runtime.CompilerServices;

namespace CysharpActions.Contexts;

/// <summary>
/// GitHub Context to access. Context is resolved by GitHub Environment Variables.
/// </summary>
public record GitHubContext
{
    public static GitHubContext Current { get; } = new GitHubContext
    {
        EventName = GitHubEnv.Current.GITHUB_EVENT_NAME,
        GitHubActions = GitHubEnv.Current.GITHUB_ACTIONS,
        Repository = GitHubEnv.Current.GITHUB_REPOSITORY,
        RunId = GitHubEnv.Current.GITHUB_RUN_ID,
        ServerUrl = GitHubEnv.Current.GITHUB_SERVER_URL
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
public record GitHubEnv
{
    public static GitHubEnv Current { get; } = new GitHubEnv();

    /// <summary>
    /// Always set to true.
    /// </summary>
    public bool CI { get; init; } = bool.Parse(Get(nameof(CI)) ?? "false");
    /// <summary>
    /// The name of the action currently running, or the id of a step. For example, for an action, __repo-owner_name-of-action-repo.<br/>
    /// GitHub removes special characters, and uses the name __run when the current step runs a script without an id.If you use the same script or action more than once in the same job, the name will include a suffix that consists of the sequence number preceded by an underscore. For example, the first script you run will have the name __run, and the second script will be named __run_2. Similarly, the second invocation of actions/checkout will be actionscheckout2.
    /// </summary>
    public string GITHUB_ACTION { get; init; } = Get(nameof(GITHUB_ACTION)) ?? "";
    /// <summary>
    /// The path where an action is located. This property is only supported in composite actions. You can use this path to change directories to where the action is located and access other files in that same repository. For example, /home/runner/work/_actions/repo-owner/name-of-action-repo/v1.
    /// </summary>
    public string GITHUB_ACTION_PATH { get; init; } = Get(nameof(GITHUB_ACTION_PATH)) ?? "";
    /// <summary>
    /// For a step executing an action, this is the owner and repository name of the action. For example, actions/checkout.
    /// </summary>
    public string GITHUB_ACTION_REPOSITORY { get; init; } = Get(nameof(GITHUB_ACTION_REPOSITORY)) ?? "";
    /// <summary>
    /// Always set to true when GitHub Actions is running the workflow. You can use this variable to differentiate when tests are being run locally or by GitHub Actions.
    /// </summary>
    public bool GITHUB_ACTIONS { get; init; } = bool.Parse(Get(nameof(GITHUB_ACTIONS)) ?? "false");
    /// <summary>
    /// The name of the person or app that initiated the workflow. For example, octocat.
    /// </summary>
    public string GITHUB_ACTOR { get; init; } = Get(nameof(GITHUB_ACTOR)) ?? "";
    /// <summary>
    /// The name of the base ref or target branch of the pull request in a workflow run. This is only set when the event that triggers a workflow run is either pull_request or pull_request_target. For example, main.
    /// </summary>
    public string GITHUB_BASE_REF { get; init; } = Get(nameof(GITHUB_BASE_REF)) ?? "";
    /// <summary>
    /// The path on the runner to the file that sets variables from workflow commands. The path to this file is unique to the current step and changes for each step in a job. For example, /home/runner/work/_temp/_runner_file_commands/set_env_87406d6e-4979-4d42-98e1-3dab1f48b13a. For more information, see Workflow commands for GitHub Actions.
    /// </summary>
    public string GITHUB_ENV { get; init; } = Get(nameof(GITHUB_ENV)) ?? "";
    /// <summary>
    /// The name of the event that triggered the workflow. For example, workflow_dispatch.
    /// </summary>
    public string GITHUB_EVENT_NAME { get; init; } = Get(nameof(GITHUB_EVENT_NAME)) ?? "";
    /// <summary>
    /// The path to the file on the runner that contains the full event webhook payload. For example, /github/workflow/event.json.
    /// </summary>
    public string GITHUB_EVENT_PATH { get; init; } = Get(nameof(GITHUB_EVENT_PATH)) ?? "";
    /// <summary>
    /// The head ref or source branch of the pull request in a workflow run. This property is only set when the event that triggers a workflow run is either pull_request or pull_request_target. For example, feature-branch-1.
    /// </summary>
    public string GITHUB_HEAD_REF { get; init; } = Get(nameof(GITHUB_HEAD_REF)) ?? "";
    /// <summary>
    /// The job_id of the current job. For example, greeting_job.
    /// </summary>
    public string GITHUB_JOB { get; init; } = Get(nameof(GITHUB_JOB)) ?? "";
    /// <summary>
    /// The path on the runner to the file that sets the current step's outputs from workflow commands. The path to this file is unique to the current step and changes for each step in a job. For example, /home/runner/work/_temp/_runner_file_commands/set_output_a50ef383-b063-46d9-9157-57953fc9f3f0. For more information, see Workflow commands for GitHub Actions.
    /// </summary>
    public string GITHUB_OUTPUT { get; init; } = Get(nameof(GITHUB_OUTPUT)) ?? "";
    /// <summary>
    /// The path on the runner to the file that sets system PATH variables from workflow commands. The path to this file is unique to the current step and changes for each step in a job. For example, /home/runner/work/_temp/_runner_file_commands/add_path_899b9445-ad4a-400c-aa89-249f18632cf5. For more information, see Workflow commands for GitHub Actions.
    /// </summary>
    public string GITHUB_PATH { get; init; } = Get(nameof(GITHUB_PATH)) ?? "";
    /// <summary>
    /// The fully-formed ref of the branch or tag that triggered the workflow run. For workflows triggered by push, this is the branch or tag ref that was pushed. For workflows triggered by pull_request, this is the pull request merge branch. For workflows triggered by release, this is the release tag created. For other triggers, this is the branch or tag ref that triggered the workflow run. This is only set if a branch or tag is available for the event type. The ref given is fully-formed, meaning that for branches the format is refs/heads/branch_name. For pull requests events except pull_request_target, it is refs/pull/pr_number/merge. pull_request_target events have the ref from the base branch. For tags it is refs/tags/tag_name. For example, refs/heads/feature-branch-1.
    /// </summary>
    public string GITHUB_REF { get; init; } = Get(nameof(GITHUB_REF)) ?? "";
    /// <summary>
    /// The short ref name of the branch or tag that triggered the workflow run. This value matches the branch or tag name shown on GitHub. For example, feature-branch-1. <br/>
    /// For pull requests, the format is pr_number/merge
    /// </summary>
    public string GITHUB_REF_NAME { get; init; } = Get(nameof(GITHUB_REF_NAME)) ?? "";
    /// <summary>
    /// The type of ref that triggered the workflow run. Valid values are branch or tag.
    /// </summary>
    public string GITHUB_REF_TYPE { get; init; } = Get(nameof(GITHUB_REF_TYPE)) ?? "";
    /// <summary>
    /// The owner and repository name. For example, octocat/Hello-World.
    /// </summary>
    public string GITHUB_REPOSITORY { get; init; } = Get(nameof(GITHUB_REPOSITORY)) ?? "";
    /// <summary>
    /// The repository owner's name. For example, octocat.
    /// </summary>
    public string GITHUB_REPOSITORY_OWNER { get; init; } = Get(nameof(GITHUB_REPOSITORY_OWNER)) ?? "";
    /// <summary>
    /// A unique number for each attempt of a particular workflow run in a repository. This number begins at 1 for the workflow run's first attempt, and increments with each re-run. For example, 3.
    /// </summary>
    public int GITHUB_RUN_ATTEMPT { get; init; } = int.Parse(Get(nameof(GITHUB_RUN_ATTEMPT)) ?? "1");
    /// <summary>
    /// A unique number for each workflow run within a repository. This number does not change if you re-run the workflow run. For example, 1658821493.
    /// </summary>
    public string GITHUB_RUN_ID { get; init; } = Get(nameof(GITHUB_RUN_ID)) ?? "";
    /// <summary>
    /// A unique number for each run of a particular workflow in a repository. This number begins at 1 for the workflow's first run, and increments with each new run. This number does not change if you re-run the workflow run. For example, 3.
    /// </summary>
    public string GITHUB_RUN_NUMBER { get; init; } = Get(nameof(GITHUB_RUN_NUMBER)) ?? "";
    /// <summary>
    /// The URL of the GitHub server. For example: https://github.com.
    /// </summary>
    public string GITHUB_SERVER_URL { get; init; } = Get(nameof(GITHUB_SERVER_URL)) ?? "";
    /// <summary>
    /// The commit SHA that triggered the workflow. The value of this commit SHA depends on the event that triggered the workflow. For more information, see Events that trigger workflows. For example, ffac537e6cbbf934b08745a378932722df287a53.
    /// </summary>
    public string GITHUB_SHA { get; init; } = Get(nameof(GITHUB_SHA)) ?? "";
    /// <summary>
    /// The username of the user that initiated the workflow run. If the workflow run is a re-run, this value may differ from github.actor. Any workflow re-runs will use the privileges of github.actor, even if the actor initiating the re-run (github.triggering_actor) has different privileges.
    /// </summary>
    public string GITHUB_TRIGGERING_ACTOR { get; init; } = Get(nameof(GITHUB_TRIGGERING_ACTOR)) ?? "";
    /// <summary>
    /// The name of the workflow. For example, My test workflow. If the workflow file doesn't specify a name, the value of this variable is the full path of the workflow file in the repository.
    /// </summary>
    public string GITHUB_WORKFLOW { get; init; } = Get(nameof(GITHUB_WORKFLOW)) ?? "";
    /// <summary>
    /// The ref path to the workflow. For example, octocat/hello-world/.github/workflows/my-workflow.yml@refs/heads/my_branch.
    /// </summary>
    public string GITHUB_WORKFLOW_REF { get; init; } = Get(nameof(GITHUB_WORKFLOW_REF)) ?? "";
    /// <summary>
    /// The default working directory on the runner for steps, and the default location of your repository when using the checkout action. For example, /home/runner/work/my-repo-name/my-repo-name.
    /// </summary>
    public string GITHUB_WORKSPACE { get; init; } = Get(nameof(GITHUB_WORKSPACE)) ?? "";
    /// <summary>
    /// The head ref or source branch of the pull request in a workflow run. This property is only set when the event that triggers a workflow run is either pull_request or pull_request_target. For example, feature-branch-1.
    /// </summary>
    public string HEAD_REF { get; init; } = Get(nameof(HEAD_REF)) ?? "";
    /// <summary>
    /// The architecture of the runner executing the job. Possible values are X86, X64, ARM, or ARM64.
    /// </summary>
    public string RUNNER_ARCH { get; init; } = Get(nameof(RUNNER_ARCH)) ?? "";
    /// <summary>
    /// This is set only if debug logging is enabled, and always has the value of 1. It can be useful as an indicator to enable additional debugging or verbose logging in your own job steps.
    /// </summary>
    public bool RUNNER_DEBUG { get; init; } = int.Parse(Get(nameof(RUNNER_DEBUG)) ?? "0") == 1;
    /// <summary>
    /// The operating system of the runner executing the job. Possible values are Linux, Windows, or macOS. For example, Windows
    /// </summary>
    public string RUNNER_OS { get; init; } = Get(nameof(RUNNER_OS)) ?? "";

    // Others

    /// <summary>
    /// Environment Variables to the PATH
    /// </summary>
    public string PATH { get; init; } = Get(nameof(PATH)) ?? "";
    /// <summary>
    /// Environment Variables to the PATH
    /// </summary>
    public bool ACTIONS_STEP_DEBUG { get; init; } = bool.Parse(Get(nameof(ACTIONS_STEP_DEBUG)) ?? "false");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? Get(string key) => Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
}

public record GHEnv
{
    public static GHEnv Current { get; } = new GHEnv();

    /// <summary>
    /// Indicate GH_xxxx is ready to use
    /// </summary>
    public bool IsGHReady => GH_REPO is not null && GH_TOKEN is not null;

    /// <summary>
    /// Get value of GH_REPO
    /// </summary>
    public string? GH_REPO { get; init; } = Get(nameof(GH_REPO));
    /// <summary>
    /// Get value of GH_TOKEN
    /// </summary>
    public string? GH_TOKEN { get; init; } = Get(nameof(GH_TOKEN));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? Get(string key) => Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
}
