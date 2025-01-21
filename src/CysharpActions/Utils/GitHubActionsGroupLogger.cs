namespace CysharpActions.Utils;

public class GitHubActionsGroupLogger : IDisposable
{
    public GitHubActionsGroupLogger(string title)
    {
        Console.WriteLine($"::group::{title}");
    }

    public void Dispose() => Console.WriteLine("::endgroup::");
}
