using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VYaml.Annotations;
using VYaml.Serialization;

namespace CysharpActions.Commands;

/// <summary>
/// Create GitHub Matrix JSON from benchmark config YAML file.
/// See README.BENCHMARK.md for spec.
/// </summary>
public partial class BenchmarkConfig2MatrixCommand(string? configPath = null)
{
    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Convert JSON to pretty print format
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static string ToPrettyPrint(string json)
    {
        var matrix = JsonSerializer.Deserialize(json, BenchmarkConfigOutputMatrixJsonContext.Default.BenchmarkConfigOutputMatrix);
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var prettyJson = JsonSerializer.Serialize(matrix, options);

        return prettyJson;
    }

    /// <summary>
    /// Generate GitHub Actions Matrix JSON from benchmark config
    /// </summary>
    /// <returns>Matrix JSON string</returns>
    /// <exception cref="ActionCommandException"></exception>
    public string GenerateMatrix()
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ActionCommandException("Config path is required. Use --config-path to specify.");
        }

        if (!File.Exists(configPath))
        {
            throw new ActionCommandException($"Config file not found: {configPath}");
        }

        var yamlBytes = System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(configPath));

        // Deserialize to typed config
        var config = YamlSerializer.Deserialize<BenchmarkJobConfig>(yamlBytes);

        if (config == null)
        {
            throw new ActionCommandException($"Failed to deserialize config: {configPath}");
        }

        // Validate
        if (config.Jobs == null || config.Jobs.Count == 0)
        {
            throw new ActionCommandException("No jobs entry found in the specified yaml file.");
        }

        // Build matrix includes
        var includes = new List<BenchmarkConfigOutputMatrix.BenchmarkMatrixInclude>();

        foreach (var job in config.Jobs)
        {
            var include = new BenchmarkConfigOutputMatrix.BenchmarkMatrixInclude
            {
                // Add non-template general keys
                AptTools = config.AptTools,
                DotnetVersion = config.DotnetVersion,
                BenchmarkLocation = config.BenchmarkLocation,
                BenchmarkExpireMin = config.BenchmarkExpireMin,
                BenchmarkTimeoutMin = config.BenchmarkTimeoutMin,
                BenchmarkClientRunScriptPath = config.BenchmarkClientRunScriptPath,
                BenchmarkServerRunScriptPath = config.BenchmarkServerRunScriptPath,
                BenchmarkServerStopScriptPath = config.BenchmarkServerStopScriptPath
            };

            // Process template string keys
            var jobProps = job.GetAllProperties();
            if (!string.IsNullOrEmpty(config.BenchmarkClientRunScriptArgs))
            {
                include.BenchmarkClientRunScriptArgs = ReplacePlaceholders(config.BenchmarkClientRunScriptArgs!, jobProps);
            }
            if (!string.IsNullOrEmpty(config.BenchmarkServerRunScriptArgs))
            {
                include.BenchmarkServerRunScriptArgs = ReplacePlaceholders(config.BenchmarkServerRunScriptArgs!, jobProps);
            }

            includes.Add(include);
        }

        var matrix = new BenchmarkConfigOutputMatrix { Include = includes.ToArray() };

        return JsonSerializer.Serialize(matrix, BenchmarkConfigOutputMatrixJsonContext.Default.BenchmarkConfigOutputMatrix);
    }

    /// <summary>
    /// Replace placeholders in template string with values from job
    /// </summary>
    private static string ReplacePlaceholders(string templateString, Dictionary<string, object> job)
    {
        var result = templateString;
        var matches = PlaceholderRegex().Matches(templateString);

        foreach (Match match in matches)
        {
            var placeholder = match.Groups[0].Value; // {{ key }}
            var key = match.Groups[1].Value; // key

            // Get value from job, use empty string if not found
            var value = job.TryGetValue(key, out var val) ? val?.ToString() ?? "" : "";

            result = result.Replace(placeholder, value);
        }

        return result;
    }
}

/// <summary>
/// Benchmark job definition
/// </summary>
[YamlObject]
public partial class BenchmarkJob
{
    // Known job properties
    [YamlMember("tags")]
    public required string Tags { get; init; }

    [YamlMember("protocol")]
    public required string Protocol { get; init; }

    [YamlMember("channels")]
    public required int? Channels { get; init; }

    [YamlMember("streams")]
    public required int? Streams { get; init; }

    [YamlMember("serialization")]
    public required string Serialization { get; init; }

    [YamlMember("buildArgsClient")]
    public required string BuildArgsClient { get; init; }

    [YamlMember("buildArgsServer")]
    public required string BuildArgsServer { get; init; }

    [YamlMember("scenario")]
    public required string Scenario { get; init; }

    /// <summary>
    /// Get all properties as dictionary for placeholder replacement
    /// </summary>
    public Dictionary<string, object> GetAllProperties()
    {
        var props = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(Tags)) props["tags"] = Tags;
        if (!string.IsNullOrEmpty(Protocol)) props["protocol"] = Protocol;
        if (Channels.HasValue) props["channels"] = Channels.Value;
        if (Streams.HasValue) props["streams"] = Streams.Value;
        if (!string.IsNullOrEmpty(Serialization)) props["serialization"] = Serialization;
        if (BuildArgsClient != null) props["buildArgsClient"] = BuildArgsClient;
        if (BuildArgsServer != null) props["buildArgsServer"] = BuildArgsServer;
        if (!string.IsNullOrEmpty(Scenario)) props["scenario"] = Scenario;

        return props;
    }
}

/// <summary>
/// Benchmark job config YAML structure
/// </summary>
[YamlObject]
public partial class BenchmarkJobConfig
{
    // Known general properties
    [YamlMember("apt-tools")]
    public required string AptTools { get; init; }

    [YamlMember("dotnet-version")]
    public required string DotnetVersion { get; init; }

    [YamlMember("benchmark-location")]
    public required string BenchmarkLocation { get; init; }

    [YamlMember("benchmark-expire-min")]
    public required int BenchmarkExpireMin { get; init; }

    [YamlMember("benchmark-timeout-min")]
    public required int BenchmarkTimeoutMin { get; init; }

    [YamlMember("benchmark-client-run-script-path")]
    public required string BenchmarkClientRunScriptPath { get; init; }

    [YamlMember("benchmark-server-run-script-path")]
    public required string BenchmarkServerRunScriptPath { get; init; }

    [YamlMember("benchmark-server-stop-script-path")]
    public required string BenchmarkServerStopScriptPath { get; init; }

    // Template string properties
    [YamlMember("benchmark-client-run-script-args")]
    public required string BenchmarkClientRunScriptArgs { get; init; }

    [YamlMember("benchmark-server-run-script-args")]
    public required string BenchmarkServerRunScriptArgs { get; init; }

    // Jobs array
    [YamlMember("jobs")]
    public required List<BenchmarkJob>? Jobs { get; init; }
}

/// <summary>
/// GitHub Actions Matrix structure with typed include items
/// </summary>
public class BenchmarkConfigOutputMatrix
{
    public required BenchmarkMatrixInclude[] Include { get; init; }

    /// <summary>
    /// Matrix include item with all benchmark configuration properties
    /// </summary>
    public class BenchmarkMatrixInclude
    {
        [JsonPropertyName("apt-tools")]
        public required string? AptTools { get; init; }

        [JsonPropertyName("dotnet-version")]
        public required string? DotnetVersion { get; init; }

        [JsonPropertyName("benchmark-location")]
        public required string? BenchmarkLocation { get; init; }

        [JsonPropertyName("benchmark-expire-min")]
        public required int? BenchmarkExpireMin { get; init; }

        [JsonPropertyName("benchmark-timeout-min")]
        public required int? BenchmarkTimeoutMin { get; init; }

        [JsonPropertyName("benchmark-client-run-script-path")]
        public required string? BenchmarkClientRunScriptPath { get; init; }

        [JsonPropertyName("benchmark-client-run-script-args")]
        public string? BenchmarkClientRunScriptArgs { get; set; }

        [JsonPropertyName("benchmark-server-run-script-path")]
        public required string? BenchmarkServerRunScriptPath { get; init; }

        [JsonPropertyName("benchmark-server-run-script-args")]
        public string? BenchmarkServerRunScriptArgs { get; set; }

        [JsonPropertyName("benchmark-server-stop-script-path")]
        public required string? BenchmarkServerStopScriptPath { get; init; }
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[System.Text.Json.Serialization.JsonSerializable(typeof(BenchmarkConfigOutputMatrix))]
internal partial class BenchmarkConfigOutputMatrixJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
