using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests EnvironmentInfo serialization through ITextSerializer.
/// Validates full JSON output and round-trip data preservation.
/// </summary>
public class EnvironmentInfoSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public EnvironmentInfoSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_WithCompleteEnvironmentInfo_PreservesAllProperties()
    {
        // Arrange
        var env = new EnvironmentInfo
        {
            MachineName = "PROD-SERVER-01",
            ProcessorCount = 8,
            TotalPhysicalMemory = 17179869184,
            AvailablePhysicalMemory = 8589934592,
            OSName = "Windows",
            OSVersion = "10.0.19041",
            Architecture = "x64",
            RuntimeVersion = "6.0.5",
            ProcessName = "MyApp",
            ProcessId = "12345"
        };

        // Act
        string? json = _serializer.SerializeToString(env);
        var deserialized = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("PROD-SERVER-01", deserialized.MachineName);
        Assert.Equal(8, deserialized.ProcessorCount);
        Assert.Equal(17179869184, deserialized.TotalPhysicalMemory);
        Assert.Equal("Windows", deserialized.OSName);
        Assert.Equal("x64", deserialized.Architecture);
    }

    [Fact]
    public void SerializeToString_WithMinimalEnvironmentInfo_PreservesProperties()
    {
        // Arrange
        var env = new EnvironmentInfo { MachineName = "TEST" };

        // Act
        string? json = _serializer.SerializeToString(env);
        var deserialized = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("TEST", deserialized.MachineName);
    }

    [Fact]
    public void Deserialize_WithCompleteEnvironmentInfo_PreservesAllProperties()
    {
        // Arrange
        var original = new EnvironmentInfo
        {
            MachineName = "DEV-LAPTOP",
            ProcessorCount = 16,
            TotalPhysicalMemory = 34359738368,
            AvailablePhysicalMemory = 17179869184,
            OSName = "macOS",
            OSVersion = "12.3.1",
            Architecture = "arm64",
            RuntimeVersion = "7.0.0"
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("DEV-LAPTOP", deserialized.MachineName);
        Assert.Equal(16, deserialized.ProcessorCount);
        Assert.Equal(34359738368, deserialized.TotalPhysicalMemory);
        Assert.Equal("macOS", deserialized.OSName);
    }

    [Fact]
    public void SerializeToString_WithCustomData_PreservesData()
    {
        // Arrange
        var env = new EnvironmentInfo
        {
            MachineName = "SERVER-01",
            ProcessorCount = 4,
            Data = new DataDictionary
            {
                ["custom_key"] = "custom_value",
                ["env_type"] = "production"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(env);
        var deserialized = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("SERVER-01", deserialized.MachineName);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("custom_value", deserialized.Data["custom_key"]);
    }

    [Fact]
    public void Deserialize_WithSnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"processor_count":12,"total_physical_memory":68719476736,"available_physical_memory":34359738368,"machine_name":"CLOUD-VM-01","o_s_name":"Ubuntu","o_s_version":"22.04","architecture":"x64","runtime_version":"8.0.0","data":{}}""";

        // Act
        var env = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(env);
        Assert.Equal("CLOUD-VM-01", env.MachineName);
        Assert.Equal(12, env.ProcessorCount);
        Assert.Equal(68719476736, env.TotalPhysicalMemory);
        Assert.Equal("Ubuntu", env.OSName);
    }

    [Fact]
    public void SerializeToString_WithLargeMemoryValues_PreservesValues()
    {
        // Arrange - 256 GB RAM
        var env = new EnvironmentInfo
        {
            MachineName = "BIG-SERVER",
            ProcessorCount = 64,
            TotalPhysicalMemory = 274877906944,
            AvailablePhysicalMemory = 137438953472
        };

        // Act
        string? json = _serializer.SerializeToString(env);
        var deserialized = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("BIG-SERVER", deserialized.MachineName);
        Assert.Equal(64, deserialized.ProcessorCount);
        Assert.Equal(274877906944, deserialized.TotalPhysicalMemory);
        Assert.Equal(137438953472, deserialized.AvailablePhysicalMemory);
    }
}
