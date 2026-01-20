using System.Text.Json;
using YamlDotNet.Serialization;

namespace CysharpActions.Commands;

/// <summary>
/// Create GitHub Matrix JSON from benchmark loader config.
/// This is equivalent to benchmark_loader2matrix.sh
/// </summary>
public class BenchmarkLoader2MatrixCommand(string benchmarkNamePrefix, string? configPath = null, string? branch = null)
{
    /// <summary>
    /// Convert JSON to pretty print format
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static string ToPrettyPrint(string json)
    {
        var matrix = JsonSerializer.Deserialize(json, BenchmarkLoaderJsonContext.Default.BenchmarkLoaderOutputMatrix);
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var prettyJson = JsonSerializer.Serialize(matrix, options);

        return prettyJson;
    }

    /// <summary>
    /// Generate GitHub Actions Matrix JSON from loader config or branch mode
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

        var yamlText = File.ReadAllText(configPath);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        var config = deserializer.Deserialize<BenchmarkLoaderConfig>(yamlText);

        if (config == null)
        {
            throw new ActionCommandException($"Failed to deserialize config: {configPath}");
        }

        var configType = config.GetConfigType();
        return configType switch
        {
            BenchmarkConfigType.Loader => GenerateLoaderMatrix(config),
            BenchmarkConfigType.Execute => GenerateBranchMatrix(),
            _ => throw new ActionCommandException($"Unknown config type: {config.Type}")
        };
    }

    /// <summary>
    /// Generate matrix from loader config
    /// </summary>
    private string GenerateLoaderMatrix(BenchmarkLoaderConfig config)
    {
        if (config.BranchConfigs == null || config.BranchConfigs.Length == 0)
        {
            throw new ActionCommandException("Loader config must have branch-configs");
        }

        // Validate: all branch-configs must have branch, config, and suffix
        foreach (var branchConfig in config.BranchConfigs)
        {
            if (string.IsNullOrWhiteSpace(branchConfig.Branch))
            {
                throw new ActionCommandException("All branch-configs must have 'branch' key");
            }
            if (string.IsNullOrWhiteSpace(branchConfig.Config))
            {
                throw new ActionCommandException("All branch-configs must have 'config' key");
            }
            if (branchConfig.Suffix == null)
            {
                throw new ActionCommandException("All branch-configs must have 'suffix' key");
            }
        }

        // Validate: all suffixes must be unique
        var suffixes = config.BranchConfigs.Select(x => x.Suffix).ToArray();
        if (suffixes.Length != suffixes.Distinct().Count())
        {
            throw new ActionCommandException("All suffixes in branch-configs must be unique");
        }

        var includes = config.BranchConfigs.Select(bc => new BenchmarkLoaderOutputMatrix.MatrixInclude
        {
            BenchmarkName = benchmarkNamePrefix + bc.Suffix,
            Branch = bc.Branch!,
            Config = bc.Config!
        }).ToArray();

        var matrix = new BenchmarkLoaderOutputMatrix { Include = includes };
        return JsonSerializer.Serialize(matrix, BenchmarkLoaderJsonContext.Default.BenchmarkLoaderOutputMatrix);
    }

    /// <summary>
    /// Generate matrix from branch mode (single benchmark)
    /// </summary>
    private string GenerateBranchMatrix()
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new ActionCommandException("Branch is required when using branch mode. Use --branch to specify.");
        }

        var includes = new[]
        {
            new BenchmarkLoaderOutputMatrix.MatrixInclude
            {
                BenchmarkName = benchmarkNamePrefix,
                Branch = branch,
                Config = configPath!
            }
        };

        var matrix = new BenchmarkLoaderOutputMatrix { Include = includes };
        return JsonSerializer.Serialize(matrix, BenchmarkLoaderJsonContext.Default.BenchmarkLoaderOutputMatrix);
    }
}

/// <summary>
/// Benchmark config type
/// </summary>
public enum BenchmarkConfigType
{
    Unknown,
    /// <summary>
    /// Generate job matrix from Loader config
    /// </summary>
    Loader,
    /// <summary>
    /// Generate job matrix from arguments
    /// </summary>
    Execute
}

/// <summary>
/// Benchmark config YAML structure
/// </summary>
public class BenchmarkLoaderConfig
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "branch-configs")]
    public required BranchConfig[] BranchConfigs { get; init; }

    /// <summary>
    /// Get parsed config type as enum
    /// </summary>
    public BenchmarkConfigType GetConfigType()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            // If type is not specified, treat as execute config
            return BenchmarkConfigType.Execute;
        }

        return Type.ToLowerInvariant() switch
        {
            "loader" => BenchmarkConfigType.Loader,
            "execute" => BenchmarkConfigType.Execute,
            _ => BenchmarkConfigType.Unknown
        };
    }

    /// <summary>
    /// Branch config structure in loader config
    /// </summary>
    public class BranchConfig
    {
        [YamlMember(Alias = "suffix")]
        public required string Suffix { get; init; }

        [YamlMember(Alias = "branch")]
        public required string Branch { get; init; }

        [YamlMember(Alias = "config")]
        public required string Config { get; init; }
    }
}

/// <summary>
/// GitHub Actions Matrix structure
/// </summary>
public class BenchmarkLoaderOutputMatrix
{
    public required MatrixInclude[] Include { get; init; }

    /// <summary>
    /// Matrix include item
    /// </summary>
    public class MatrixInclude
    {
        public required string BenchmarkName { get; init; }
        public required string Branch { get; init; }
        public required string Config { get; init; }
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[System.Text.Json.Serialization.JsonSerializable(typeof(BenchmarkLoaderOutputMatrix))]
internal partial class BenchmarkLoaderJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
