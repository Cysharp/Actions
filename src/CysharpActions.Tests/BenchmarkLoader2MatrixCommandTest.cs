using System.Text.Json;

namespace CysharpActions.Tests;

public class BenchmarkLoader2MatrixCommandTest
{
    [Fact]
    public void LoaderMode_ValidConfig_GeneratesCorrectMatrix()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(LoaderMode_ValidConfig_GeneratesCorrectMatrix)}";
        var configPath = $"{baseDirectory}/loader_config.yaml";
        var configContent = """
            type: loader
            branch-configs:
              - suffix: ""
                branch: main
                config: ./perf/benchmark1.yaml
              - suffix: "-1"
                branch: develop
                config: ./perf/benchmark2.yaml
            """;

        var expectedResultJson = """
        {
            "include": [
            {
                "benchmarkName": "test-benchmark",
                "branch": "main",
                "config": "./perf/benchmark1.yaml"
            },
            {
                "benchmarkName": "test-benchmark-1",
                "branch": "develop",
                "config": "./perf/benchmark2.yaml"
            }
            ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var expectedMatrix = JsonSerializer.Deserialize<BenchmarkJobMatrix>(expectedResultJson, options);
        Assert.NotNull(expectedMatrix);
        Assert.NotNull(expectedMatrix.Include);

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark", configPath);
            var result = command.GenerateMatrix();

            // Assert
            var matrix = JsonSerializer.Deserialize<BenchmarkJobMatrix>(result, options);
            Assert.NotNull(matrix);
            Assert.NotNull(matrix.Include);
            Assert.Equal(expectedMatrix.Include.Length, matrix.Include.Length);

            // Compare each item in detail
            for (int i = 0; i < expectedMatrix.Include.Length; i++)
            {
                Assert.Equal(expectedMatrix.Include[i].BenchmarkName, matrix.Include[i].BenchmarkName);
                Assert.Equal(expectedMatrix.Include[i].Branch, matrix.Include[i].Branch);
                Assert.Equal(expectedMatrix.Include[i].Config, matrix.Include[i].Config);
            }

            // Verify by re-serializing both and comparing JSON strings (normalized)
            var expectedJsonNormalized = JsonSerializer.Serialize(expectedMatrix, options);
            var actualJsonNormalized = JsonSerializer.Serialize(matrix, options);
            Assert.Equal(expectedJsonNormalized, actualJsonNormalized);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void BranchMode_ValidConfig_GeneratesCorrectMatrix()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(BranchMode_ValidConfig_GeneratesCorrectMatrix)}";
        var configPath = $"{baseDirectory}/execute_config.yaml";
        var configContent = """
            apt-tools: libmsquic
            dotnet-version: 8.0
            benchmark-expire-min: 15
            jobs:
              - tags: test
                protocol: h2c
            """;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Expected matrix for comparison (Config path is dynamic, so we build the expected result)
            var expectedMatrix = new BenchmarkJobMatrix
            {
                Include =
                [
                    new MatrixInclude
                    {
                        BenchmarkName = "test-benchmark-123",
                        Branch = "feature/test",
                        Config = configPath
                    }
                ]
            };

            // Act
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark-123", configPath, "feature/test");
            var result = command.GenerateMatrix();

            // Assert
            var matrix = JsonSerializer.Deserialize<BenchmarkJobMatrix>(result, options);
            Assert.NotNull(matrix);
            Assert.NotNull(matrix.Include);
            Assert.Equal(expectedMatrix.Include.Length, matrix.Include.Length);

            // Compare each item in detail
            for (int i = 0; i < expectedMatrix.Include.Length; i++)
            {
                Assert.Equal(expectedMatrix.Include[i].BenchmarkName, matrix.Include[i].BenchmarkName);
                Assert.Equal(expectedMatrix.Include[i].Branch, matrix.Include[i].Branch);
                Assert.Equal(expectedMatrix.Include[i].Config, matrix.Include[i].Config);
            }

            // Verify by re-serializing both and comparing JSON strings (normalized)
            var expectedJsonNormalized = JsonSerializer.Serialize(expectedMatrix, options);
            var actualJsonNormalized = JsonSerializer.Serialize(matrix, options);
            Assert.Equal(expectedJsonNormalized, actualJsonNormalized);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void LoaderMode_DuplicateSuffix_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(LoaderMode_DuplicateSuffix_ThrowsException)}";
        var configPath = $"{baseDirectory}/invalid_config.yaml";
        var configContent = """
            type: loader
            branch-configs:
              - suffix: "-1"
                branch: main
                config: ./perf/benchmark1.yaml
              - suffix: "-1"
                branch: develop
                config: ./perf/benchmark2.yaml
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark", configPath);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void LoaderMode_MissingBranch_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(LoaderMode_MissingBranch_ThrowsException)}";
        var configPath = $"{baseDirectory}/invalid_config.yaml";
        var configContent = """
            type: loader
            branch-configs:
              - suffix: ""
                config: ./perf/benchmark1.yaml
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark", configPath);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("branch", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void LoaderMode_MissingConfig_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(LoaderMode_MissingConfig_ThrowsException)}";
        var configPath = $"{baseDirectory}/invalid_config.yaml";
        var configContent = """
            type: loader
            branch-configs:
              - suffix: ""
                branch: main
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark", configPath);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("config", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void LoaderMode_MissingSuffix_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(LoaderMode_MissingSuffix_ThrowsException)}";
        var configPath = $"{baseDirectory}/invalid_config.yaml";
        var configContent = """
            type: loader
            branch-configs:
              - branch: main
                config: ./perf/benchmark1.yaml
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark", configPath);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("suffix", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void BranchMode_MissingBranch_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkLoader2MatrixCommandTest)}/{nameof(BranchMode_MissingBranch_ThrowsException)}";
        var configPath = $"{baseDirectory}/execute_config.yaml";
        var configContent = """
            apt-tools: libmsquic
            dotnet-version: 8.0
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkLoader2MatrixCommand("test-benchmark", configPath, null);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("branch", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void MissingConfigPath_ThrowsException()
    {
        // Act & Assert
        var command = new BenchmarkLoader2MatrixCommand("test-benchmark", null, null);
        var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
        Assert.Contains("config path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonExistentConfigFile_ThrowsException()
    {
        // Act & Assert
        var command = new BenchmarkLoader2MatrixCommand("test-benchmark", "/non/existent/path.yaml", null);
        var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityTest_WithTemplateScheduleLoader()
    {
        // This test validates compatibility with the existing Bash script using the same template file
        var templatePath = "../../../../../.github/scripts/tests/template_schedule_loader.yaml";
        Assert.True(File.Exists(templatePath), "Template file for compatibility test not found.");

        var expectedResultJson = """
        {
            "include": [
            {
                "benchmarkName": "foo",
                "branch": "main",
                "config": "./.github/scripts/tests/template_benchmark_config.yaml"
            },
            {
                "benchmarkName": "foo-1",
                "branch": "feature/schedule",
                "config": "./.github/scripts/tests/template_benchmark_config.yaml"
            }
            ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var expectedMatrix = JsonSerializer.Deserialize<BenchmarkJobMatrix>(expectedResultJson, options);
        Assert.NotNull(expectedMatrix);
        Assert.NotNull(expectedMatrix.Include);

        // Act
        var command = new BenchmarkLoader2MatrixCommand("foo", templatePath);
        string? result = command.GenerateMatrix();

        // Assert - Verify the structure matches what bash script would generate
        var matrix = JsonSerializer.Deserialize<BenchmarkJobMatrix>(result, options);
        Assert.NotNull(matrix);
        Assert.NotNull(matrix.Include);

        Assert.Equal(expectedMatrix.Include.Length, matrix.Include.Length);
        // Compare each item in detail
        for (int i = 0; i < expectedMatrix.Include.Length; i++)
        {
            Assert.Equal(expectedMatrix.Include[i].BenchmarkName, matrix.Include[i].BenchmarkName);
            Assert.Equal(expectedMatrix.Include[i].Branch, matrix.Include[i].Branch);
            Assert.Equal(expectedMatrix.Include[i].Config, matrix.Include[i].Config);
        }

        // Also verify by re-serializing both and comparing JSON strings (normalized)
        var expectedJsonNormalized = JsonSerializer.Serialize(expectedMatrix, options);
        var actualJsonNormalized = JsonSerializer.Serialize(matrix, options);
        Assert.Equal(expectedJsonNormalized, actualJsonNormalized);
    }
}
