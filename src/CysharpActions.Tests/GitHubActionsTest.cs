using CysharpActions.Utils;

namespace CysharpActions.Tests;

[Collection("Console output")]
public class GitHubActionsTest
{
    [Fact]
    public void WriteRedactedRawLogRedactsMultipleSecretsTest()
    {
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);

            GitHubActions.WriteRedactedRawLog("secret-1 visible secret-2", "secret-1", "secret-2", "");

            Assert.Equal("*** visible ***", output.ToString().TrimEnd());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
