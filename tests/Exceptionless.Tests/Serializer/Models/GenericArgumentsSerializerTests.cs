using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests GenericArguments serialization through ITextSerializer.
/// GenericArguments extends Collection&lt;string&gt; directly.
/// </summary>
public class GenericArgumentsSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public GenericArgumentsSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesAllArguments()
    {
        // Arrange
        var args = new GenericArguments { "TEvent", "TResult", "CancellationToken" };

        // Act
        string json = _serializer.SerializeToString(args);
        var deserialized = _serializer.Deserialize<GenericArguments>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        Assert.Equal("TEvent", deserialized[0]);
        Assert.Equal("TResult", deserialized[1]);
        Assert.Equal("CancellationToken", deserialized[2]);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyCollection()
    {
        // Arrange
        /* language=json */
        const string json = """[]""";

        // Act
        var args = _serializer.Deserialize<GenericArguments>(json);

        // Assert
        Assert.NotNull(args);
        Assert.Empty(args);
    }

    [Fact]
    public void Deserialize_SingleArgument_RoundTrips()
    {
        // Arrange
        var args = new GenericArguments { "Task`1" };

        // Act
        string json = _serializer.SerializeToString(args);
        var deserialized = _serializer.Deserialize<GenericArguments>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal("Task`1", deserialized[0]);
    }
}
