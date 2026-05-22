using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class MethodSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public MethodSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"is_signature_target":true,"declaring_namespace":"System","declaring_type":"String","name":"Format","module_id":1,"generic_arguments":["T"],"parameters":[{"name":"format","type":"String","type_namespace":"System"}]}""";

        // Act
        var result = _serializer.Deserialize<Method>(json);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSignatureTarget);
        Assert.Equal("System", result.DeclaringNamespace);
        Assert.Equal("String", result.DeclaringType);
        Assert.Equal("Format", result.Name);
        Assert.Equal(1, result.ModuleId);
        Assert.Single(result.GenericArguments!);
        Assert.Single(result.Parameters!);
    }



    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        // Arrange
        var method = new Method
        {
            IsSignatureTarget = true,
            DeclaringNamespace = "Exceptionless.Core",
            DeclaringType = "EventProcessor",
            Name = "ProcessAsync",
            ModuleId = 42,
            Data = new DataDictionary { ["il_offset"] = 128 },
            GenericArguments = ["TEvent", "TResult"],
            Parameters = [
                new Parameter { Name = "ev", Type = "PersistentEvent", TypeNamespace = "Exceptionless.Core.Models" },
                new Parameter { Name = "token", Type = "CancellationToken", TypeNamespace = "System.Threading" }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(method);
        var result = _serializer.Deserialize<Method>(json);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSignatureTarget);
        Assert.Equal("Exceptionless.Core", result.DeclaringNamespace);
        Assert.Equal("EventProcessor", result.DeclaringType);
        Assert.Equal("ProcessAsync", result.Name);
        Assert.Equal(42, result.ModuleId);
        Assert.NotNull(result.GenericArguments);
        Assert.Equal(2, result.GenericArguments.Count);
        Assert.Equal("TEvent", result.GenericArguments[0]);
        Assert.NotNull(result.Parameters);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("ev", result.Parameters[0].Name);
        Assert.Equal("PersistentEvent", result.Parameters[0].Type);
    }

    [Fact]
    public void RoundTrip_WithMinimalProperties_PreservesValues()
    {
        // Arrange
        var method = new Method { Name = "Main" };

        // Act
        string? json = _serializer.SerializeToString(method);
        var result = _serializer.Deserialize<Method>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Main", result.Name);
        Assert.Null(result.DeclaringNamespace);
        Assert.Null(result.IsSignatureTarget);
    }
}
