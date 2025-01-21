namespace CysharpActions;

public enum VersionIncrement
{
    Major,
    Minor,
    Patch,
    //Prerelease, // TODO: how to calculate count since last tag?
}

public enum OutputFormatType
{
    Console,
    GitHubActionsOutput,
}