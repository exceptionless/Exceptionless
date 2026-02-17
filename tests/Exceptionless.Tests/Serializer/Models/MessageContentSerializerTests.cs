using Exceptionless.Web.Utility.Results;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests MessageContent serialization through ITextSerializer.
/// MessageContent is a record returned from API endpoints.
/// Validates that the record primary constructor properties serialize correctly.
/// </summary>
public class MessageContentSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public MessageContentSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesProperties()
    {
        // Arrange
        var message = new MessageContent("id123", "Something happened");

        // Act
        string json = _serializer.SerializeToString(message);
        var deserialized = _serializer.Deserialize<MessageContent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("id123", deserialized.Id);
        Assert.Equal("Something happened", deserialized.Message);
    }

    [Fact]
    public void Deserialize_RoundTrip_WithNullId_PreservesMessage()
    {
        // Arrange
        var message = new MessageContent("Operation completed successfully");

        // Act
        string json = _serializer.SerializeToString(message);
        var deserialized = _serializer.Deserialize<MessageContent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Id);
        Assert.Equal("Operation completed successfully", deserialized.Message);
    }
}
