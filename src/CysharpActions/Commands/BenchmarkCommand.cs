using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CysharpActions.Commands;

public class BenchmarkCommand(string devCenterName, string projectName)
{
    public async Task Cleanup(string state, bool dryRun, bool tryRedeploy, bool noDelete)
    {
        Env.useShell = false;

    }

    public class DevCeneterEnvironment(string devCenterName, string projectName)
    {
        public async Task Redeploy(string name, string userId, DateTimeOffset expirationDate)
        {
            Env.useShell = false;
            var date = expirationDate; // 2024-07-24T05:31:52Z format
            var parameter = ""; // "$(jq -c -n --arg n "$n" --argjson minimize true '{name: $n, minimize: $minimize}')"
            await $"az devcenter dev environment deploy --dev-center-name \"{devCenterName}\" --project-name \"{projectName}\" --name \"{name}\" --parameters \"<TODO>\" --expiration-date \"{date}\" --user-id \"{userId}\"";
        }
        public async Task Delete(string name, string userId)
        {
            Env.useShell = false;
            await $"az devcenter dev environment delete --dev-center-name \"{devCenterName}\" --project-name \"{projectName}\" --name \"{name}\" --user-id \"{userId}\" --yes --no-wait";
        }
        public async Task Extend(string name, string userId, DateTimeOffset expirationDate)
        {
            Env.useShell = false;
            var date = expirationDate; // 2024-07-24T05:31:52Z format
            var parameter = ""; // $(jq -c -n --arg n \"$n\" --argjson minimize true '{name: $n, minimize: $minimize}')
            await $"az devcenter dev environment deploy --dev-center-name \"{devCenterName}\" --project-name \"{projectName}\" --name \"{name}\" --parameters \"{parameter}\" --expiration-date \"{date}\" --user-id \"{userId}\"";
        }
        public async Task List(string state)
        {

        }
    }
}