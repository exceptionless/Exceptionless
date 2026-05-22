using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class SavedViewSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public SavedViewSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"770000000000000000000003","organization_id":"550000000000000000000001","created_by_user_id":"660000000000000000000001","is_default":false,"name":"Error Stream","view_type":"stream","version":1,"created_utc":"2024-02-20T14:30:00Z","updated_utc":"2024-02-20T14:30:00Z"}""";

        // Act
        var result = _serializer.Deserialize<SavedView>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("770000000000000000000003", result.Id);
        Assert.Equal("Error Stream", result.Name);
        Assert.Equal("stream", result.ViewType);
        Assert.Equal("660000000000000000000001", result.CreatedByUserId);
    }

    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        // Arrange
        var view = new SavedView
        {
            Id = "770000000000000000000001",
            OrganizationId = "550000000000000000000001",
            UserId = "660000000000000000000001",
            CreatedByUserId = "660000000000000000000001",
            UpdatedByUserId = "660000000000000000000002",
            Filter = "(status:open OR status:regressed)",
            FilterDefinitions = """[{"field":"status","operator":"in","values":["open","regressed"]}]""",
            Columns = new Dictionary<string, bool>
            {
                ["title"] = true,
                ["date"] = true,
                ["status"] = false
            },
            Name = "Open Issues",
            Time = "[now-7d TO now]",
            Version = 1,
            ViewType = "issues",
            CreatedUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2024, 6, 1, 8, 30, 0, DateTimeKind.Utc)
        };

        // Act
        string? json = _serializer.SerializeToString(view);
        var result = _serializer.Deserialize<SavedView>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("770000000000000000000001", result.Id);
        Assert.Equal("550000000000000000000001", result.OrganizationId);
        Assert.Equal("660000000000000000000001", result.UserId);
        Assert.Equal("660000000000000000000001", result.CreatedByUserId);
        Assert.Equal("660000000000000000000002", result.UpdatedByUserId);
        Assert.Equal("(status:open OR status:regressed)", result.Filter);
        Assert.Equal("Open Issues", result.Name);
        Assert.Equal("[now-7d TO now]", result.Time);
        Assert.Equal("issues", result.ViewType);
        Assert.NotNull(result.Columns);
        Assert.Equal(3, result.Columns.Count);
        Assert.True(result.Columns["title"]);
        Assert.False(result.Columns["status"]);
    }

    [Fact]
    public void RoundTrip_WithMinimalProperties_PreservesValues()
    {
        // Arrange
        var view = new SavedView
        {
            Id = "770000000000000000000002",
            OrganizationId = "550000000000000000000001",
            CreatedByUserId = "660000000000000000000001",
            Name = "All Events",
            ViewType = "events",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        string? json = _serializer.SerializeToString(view);
        var result = _serializer.Deserialize<SavedView>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("All Events", result.Name);
        Assert.Equal("events", result.ViewType);
        Assert.Null(result.UserId);
        Assert.Null(result.Filter);
        Assert.Null(result.Time);
    }
}
