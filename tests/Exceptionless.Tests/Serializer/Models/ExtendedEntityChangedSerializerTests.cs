using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests ExtendedEntityChanged serialization through ITextSerializer.
/// ExtendedEntityChanged has private set properties and a private constructor.
/// It is created via the Create() factory method but goes through message bus
/// serialization (ISerializer → STJ) in production (Redis).
/// </summary>
public class ExtendedEntityChangedSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ExtendedEntityChangedSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesProperties()
    {
        // Arrange
        var entityChanged = new EntityChanged
        {
            Id = "abc123",
            Type = typeof(Project).Name,
            ChangeType = ChangeType.Saved,
            Data = { { "OrganizationId", "org1" }, { "ProjectId", "proj1" }, { "StackId", "stack1" } }
        };
        var model = ExtendedEntityChanged.Create(entityChanged);

        // Act
        string json = _serializer.SerializeToString(model);
        var deserialized = _serializer.Deserialize<ExtendedEntityChanged>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("abc123", deserialized.Id);
        Assert.Equal("org1", deserialized.OrganizationId);
        Assert.Equal("proj1", deserialized.ProjectId);
        Assert.Equal("stack1", deserialized.StackId);
    }

    [Fact]
    public void Deserialize_RoundTrip_WithPartialData_PreservesAvailableProperties()
    {
        // Arrange — not all entity changes have all three IDs
        var entityChanged = new EntityChanged
        {
            Id = "def456",
            Type = typeof(Project).Name,
            ChangeType = ChangeType.Removed,
            Data = { { "OrganizationId", "org1" } }
        };
        var model = ExtendedEntityChanged.Create(entityChanged);

        // Act
        string json = _serializer.SerializeToString(model);
        var deserialized = _serializer.Deserialize<ExtendedEntityChanged>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("def456", deserialized.Id);
        Assert.Equal("org1", deserialized.OrganizationId);
        Assert.Null(deserialized.ProjectId);
        Assert.Null(deserialized.StackId);
    }
}
