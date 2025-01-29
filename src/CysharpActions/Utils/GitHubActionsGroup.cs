namespace CysharpActions.Utils;

public class GitHubActionsGroup : IDisposable
{
    public GitHubActionsGroup(string title)
    {
        Console.WriteLine($"::group::{title}");
    }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    {
        Console.WriteLine("::endgroup::");
    }
}
