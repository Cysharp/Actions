using System.Text.Json;

namespace CysharpActions.Tests;

public class BenchmarkConfig2MatrixCommandTest
{
    [Fact]
    public void ValidConfig_GeneratesCorrectMatrix()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkConfig2MatrixCommandTest)}/{nameof(ValidConfig_GeneratesCorrectMatrix)}";
        var configPath = $"{baseDirectory}/benchmark_config.yaml";
        var configContent = """
            apt-tools: libmsquic
            dotnet-version: 8.0
            benchmark-expire-min: 15
            benchmark-client-run-script-args: '--run-args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol {{ protocol }} --channels {{ channels }}" --build-args "{{ buildArgsClient }}"'
            benchmark-server-run-script-args: '--run-args "-u http://0.0.0.0:5000 --protocol {{ protocol }} --scenario {{ scenario }}" --build-args "{{ buildArgsServer }}"'
            jobs:
              - protocol: h2c
                channels: 28
                buildArgsClient: "--p:UseNuGetClient=6.14"
                buildArgsServer: ""
                scenario: CI
              - protocol: grpc
                channels: 1
                buildArgsClient: ""
                buildArgsServer: "--p:UseNuGetServer=6.14"
                scenario: CI
              - protocol: h2
                channels: 100
                session: 1
                buildArgsClient: ""
                buildArgsServer: "--p:UseNuGetServer=6.14"
                scenario: Broadcast60Fps
            """;

        var expectedResultJson = """
        {
            "include": [
                {
                    "apt-tools": "libmsquic",
                    "dotnet-version": "8.0",
                    "benchmark-location": null,
                    "benchmark-expire-min": 15,
                    "benchmark-timeout-min": 0,
                    "benchmark-client-run-script-path": null,
                    "benchmark-server-run-script-path": null,
                    "benchmark-server-stop-script-path": null,
                    "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c --channels 28\" --build-args \"--p:UseNuGetClient=6.14\"",
                    "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol h2c --scenario CI\" --build-args \"\""
                },
                {
                    "apt-tools": "libmsquic",
                    "dotnet-version": "8.0",
                    "benchmark-location": null,
                    "benchmark-expire-min": 15,
                    "benchmark-timeout-min": 0,
                    "benchmark-client-run-script-path": null,
                    "benchmark-server-run-script-path": null,
                    "benchmark-server-stop-script-path": null,
                    "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol grpc --channels 1\" --build-args \"\"",
                    "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol grpc --scenario CI\" --build-args \"--p:UseNuGetServer=6.14\""
                },
                {
                    "apt-tools": "libmsquic",
                    "dotnet-version": "8.0",
                    "benchmark-location": null,
                    "benchmark-expire-min": 15,
                    "benchmark-timeout-min": 0,
                    "benchmark-client-run-script-path": null,
                    "benchmark-server-run-script-path": null,
                    "benchmark-server-stop-script-path": null,
                    "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2 --channels 100\" --build-args \"\"",
                    "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol h2 --scenario Broadcast60Fps\" --build-args \"--p:UseNuGetServer=6.14\""
                }
            ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var expectedMatrix = JsonSerializer.Deserialize<BenchmarkConfigOutputMatrix>(expectedResultJson, options);
        Assert.NotNull(expectedMatrix);
        Assert.NotNull(expectedMatrix.Include);

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act
            var command = new BenchmarkConfig2MatrixCommand(configPath);
            var result = command.GenerateMatrix();

            // Assert
            var matrix = JsonSerializer.Deserialize<BenchmarkConfigOutputMatrix>(result, options);
            Assert.NotNull(matrix);
            Assert.NotNull(matrix.Include);
            Assert.Equal(expectedMatrix.Include.Length, matrix.Include.Length);

            // Compare each item in detail
            for (int i = 0; i < expectedMatrix.Include.Length; i++)
            {
                Assert.Equal(expectedMatrix.Include[i].AptTools, matrix.Include[i].AptTools);
                Assert.Equal(expectedMatrix.Include[i].DotnetVersion, matrix.Include[i].DotnetVersion);
                Assert.Equal(expectedMatrix.Include[i].BenchmarkExpireMin, matrix.Include[i].BenchmarkExpireMin);
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
    public void MissingPlaceholder_ReplacesWithEmptyString()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkConfig2MatrixCommandTest)}/{nameof(MissingPlaceholder_ReplacesWithEmptyString)}";
        var configPath = $"{baseDirectory}/benchmark_config.yaml";
        var configContent = """
            benchmark-client-run-script-args: '--protocol {{ protocol }} --missing {{ missingKey }}'
            jobs:
              - protocol: h2c
            """;

        var expectedResultJson = """
        {
            "include": [
              {
                "apt-tools": null,
                "dotnet-version": null,
                "benchmark-location": null,
                "benchmark-expire-min": 0,
                "benchmark-timeout-min": 0,
                "benchmark-client-run-script-path": null,
                "benchmark-server-run-script-path": null,
                "benchmark-server-stop-script-path": null,
                "benchmark-client-run-script-args": "--protocol h2c --missing "
              }
            ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var expectedMatrix = JsonSerializer.Deserialize<BenchmarkConfigOutputMatrix>(expectedResultJson, options);
        Assert.NotNull(expectedMatrix);
        Assert.NotNull(expectedMatrix.Include);

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act
            var command = new BenchmarkConfig2MatrixCommand(configPath);
            var result = command.GenerateMatrix();

            // Assert
            var matrix = JsonSerializer.Deserialize<BenchmarkConfigOutputMatrix>(result, options);
            Assert.NotNull(matrix);
            Assert.NotNull(matrix.Include);
            Assert.Equal(expectedMatrix.Include.Length, matrix.Include.Length);

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
    public void MissingConfigPath_ThrowsException()
    {
        // Act & Assert
        var command = new BenchmarkConfig2MatrixCommand(null);
        var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
        Assert.Contains("config path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonExistentConfigFile_ThrowsException()
    {
        // Act & Assert
        var command = new BenchmarkConfig2MatrixCommand("/non/existent/path.yaml");
        var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingJobs_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkConfig2MatrixCommandTest)}/{nameof(MissingJobs_ThrowsException)}";
        var configPath = $"{baseDirectory}/benchmark_config.yaml";
        var configContent = """
            apt-tools: libmsquic
            dotnet-version: 8.0
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkConfig2MatrixCommand(configPath);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("jobs", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void EmptyJobs_ThrowsException()
    {
        // Arrange
        var baseDirectory = $".tests/{nameof(BenchmarkConfig2MatrixCommandTest)}/{nameof(EmptyJobs_ThrowsException)}";
        var configPath = $"{baseDirectory}/benchmark_config.yaml";
        var configContent = """
            apt-tools: libmsquic
            jobs: []
            """;

        try
        {
            TestHelper.CreateFile(configPath, configContent);

            // Act & Assert
            var command = new BenchmarkConfig2MatrixCommand(configPath);
            var exception = Assert.Throws<ActionCommandException>(() => command.GenerateMatrix());
            Assert.Contains("jobs", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelper.SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void CompatibilityTest_WithTemplateBenchmarkConfig()
    {
        // This test validates compatibility with the existing Bash script using the same template file
        var templatePath = "../../../../../.github/scripts/tests/template_benchmark_config.yaml";
        if (!File.Exists(templatePath))
        {
            // Skip test if template file doesn't exist
            return;
        }

        var expectedResultJson = """
        {
          "include": [
            {
              "apt-tools": "libmsquic",
              "dotnet-version": "8.0",
              "benchmark-location": "japaneast",
              "benchmark-expire-min": 15,
              "benchmark-timeout-min": 10,
              "benchmark-client-run-script-path": ".github/scripts/benchmark-client-run.sh",
              "benchmark-server-run-script-path": ".github/scripts/benchmark-server-run.sh",
              "benchmark-server-stop-script-path": ".github/scripts/benchmark-server-stop.sh",
              "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"\"",
              "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"\""
            },
            {
              "apt-tools": "libmsquic",
              "dotnet-version": "8.0",
              "benchmark-location": "japaneast",
              "benchmark-expire-min": 15,
              "benchmark-timeout-min": 10,
              "benchmark-client-run-script-path": ".github/scripts/benchmark-client-run.sh",
              "benchmark-server-run-script-path": ".github/scripts/benchmark-server-run.sh",
              "benchmark-server-stop-script-path": ".github/scripts/benchmark-server-stop.sh",
              "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 1 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c\" --build-args \"\"",
              "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c\" --build-args \"\""
            },
            {
              "apt-tools": "libmsquic",
              "dotnet-version": "8.0",
              "benchmark-location": "japaneast",
              "benchmark-expire-min": 15,
              "benchmark-timeout-min": 10,
              "benchmark-client-run-script-path": ".github/scripts/benchmark-client-run.sh",
              "benchmark-server-run-script-path": ".github/scripts/benchmark-server-run.sh",
              "benchmark-server-stop-script-path": ".github/scripts/benchmark-server-stop.sh",
              "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"--p:UseNuGetClient=6.14\"",
              "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"\""
            },
            {
              "apt-tools": "libmsquic",
              "dotnet-version": "8.0",
              "benchmark-location": "japaneast",
              "benchmark-expire-min": 15,
              "benchmark-timeout-min": 10,
              "benchmark-client-run-script-path": ".github/scripts/benchmark-client-run.sh",
              "benchmark-server-run-script-path": ".github/scripts/benchmark-server-run.sh",
              "benchmark-server-stop-script-path": ".github/scripts/benchmark-server-stop.sh",
              "benchmark-client-run-script-args": "--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"\"",
              "benchmark-server-run-script-args": "--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"--p:UseNuGetServer=6.14\""
            }
          ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var expectedMatrix = JsonSerializer.Deserialize<BenchmarkConfigOutputMatrix>(expectedResultJson, options);
        Assert.NotNull(expectedMatrix);
        Assert.NotNull(expectedMatrix.Include);

        // Act
        var command = new BenchmarkConfig2MatrixCommand(templatePath);
        var result = command.GenerateMatrix();

        // Assert - Verify the structure matches what bash script would generate
        var matrix = JsonSerializer.Deserialize<BenchmarkConfigOutputMatrix>(result, options);
        Assert.NotNull(matrix);
        Assert.NotNull(matrix.Include);

        // The template has 4 jobs
        Assert.Equal(4, matrix.Include.Length);

        // Each include should have the general keys
        for (int i = 0; i < matrix.Include.Length; i++)
        {
            var include = matrix.Include[i];
            var expectedInclude = expectedMatrix.Include[i];
            Assert.Equal(expectedInclude.AptTools, include.AptTools);
            Assert.Equal(expectedInclude.DotnetVersion, include.DotnetVersion);
            Assert.Equal(expectedInclude.BenchmarkLocation, include.BenchmarkLocation);
            Assert.Equal(expectedInclude.BenchmarkExpireMin, include.BenchmarkExpireMin);
            Assert.Equal(expectedInclude.BenchmarkTimeoutMin, include.BenchmarkTimeoutMin);
            Assert.Equal(expectedInclude.BenchmarkClientRunScriptPath, include.BenchmarkClientRunScriptPath);
            Assert.Equal(expectedInclude.BenchmarkServerRunScriptPath, include.BenchmarkServerRunScriptPath);
            Assert.Equal(expectedInclude.BenchmarkServerStopScriptPath, include.BenchmarkServerStopScriptPath);
        }

        // Verify placeholders were replaced (check first job)
        var firstInclude = matrix.Include[0];
        var clientArgs = firstInclude.BenchmarkClientRunScriptArgs ?? "";

        // Should not contain placeholder syntax
        Assert.DoesNotContain("{{", clientArgs);
        Assert.DoesNotContain("}}", clientArgs);
        // Should contain replaced values
        Assert.Contains("h2c", clientArgs);
    }
}
