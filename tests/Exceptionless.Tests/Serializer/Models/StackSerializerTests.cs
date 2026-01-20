using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests Stack (error group) serialization through ITextSerializer.
/// Validates round-trip serialization and snake_case property naming.
/// </summary>
public class StackSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private static readonly DateTime FixedDateTime = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    public StackSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_Stack_PreservesAllProperties()
    {
        // Arrange
        var stack = new Stack
        {
            Id = "stack123",
            OrganizationId = "org456",
            ProjectId = "proj789",
            Type = Event.KnownTypes.Error,
            SignatureHash = "abc123",
            FirstOccurrence = FixedDateTime,
            LastOccurrence = FixedDateTime,
            TotalOccurrences = 42
        };

        // Act
        string? json = _serializer.SerializeToString(stack);
        var deserialized = _serializer.Deserialize<Stack>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("stack123", deserialized.Id);
        Assert.Equal("org456", deserialized.OrganizationId);
        Assert.Equal("proj789", deserialized.ProjectId);
        Assert.Equal(Event.KnownTypes.Error, deserialized.Type);
        Assert.Equal("abc123", deserialized.SignatureHash);
        Assert.Equal(42, deserialized.TotalOccurrences);
    }

    [Fact]
    public void Deserialize_CompleteStack_PreservesAllProperties()
    {
        // Arrange
        var original = new Stack
        {
            Id = "stack123",
            OrganizationId = "org456",
            ProjectId = "proj789",
            Type = Event.KnownTypes.Error,
            Title = "NullReferenceException in UserService.GetUser",
            Status = StackStatus.Open,
            SignatureHash = "abc123def456",
            FirstOccurrence = FixedDateTime.AddDays(-7),
            LastOccurrence = FixedDateTime,
            TotalOccurrences = 42,
            Tags = new TagSet(["production", "critical"]),
            CreatedUtc = FixedDateTime.AddDays(-7),
            UpdatedUtc = FixedDateTime
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Stack>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("stack123", deserialized.Id);
        Assert.Equal("org456", deserialized.OrganizationId);
        Assert.Equal("proj789", deserialized.ProjectId);
        Assert.Equal(Event.KnownTypes.Error, deserialized.Type);
        Assert.Equal("NullReferenceException in UserService.GetUser", deserialized.Title);
        Assert.Equal(StackStatus.Open, deserialized.Status);
        Assert.Equal(42, deserialized.TotalOccurrences);
    }

    [Fact]
    public void Deserialize_StackWithAllStatuses_PreservesStatus()
    {
        // Arrange
        var statuses = new[] { StackStatus.Open, StackStatus.Fixed, StackStatus.Regressed, StackStatus.Ignored, StackStatus.Discarded };

        foreach (var status in statuses)
        {
            var original = new Stack
            {
                Id = $"stack-{status}",
                OrganizationId = "org1",
                ProjectId = "proj1",
                Type = Event.KnownTypes.Error,
                SignatureHash = "hash1",
                Status = status,
                FirstOccurrence = FixedDateTime,
                LastOccurrence = FixedDateTime
            };

            // Act
            string? json = _serializer.SerializeToString(original);
            var deserialized = _serializer.Deserialize<Stack>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(status, deserialized.Status);
        }
    }

    [Fact]
    public void Deserialize_StackWithTags_PreservesTags()
    {
        // Arrange
        var original = new Stack
        {
            Id = "stack-tags",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Error,
            SignatureHash = "hash1",
            Tags = new TagSet(["api", "auth", "critical"]),
            FirstOccurrence = FixedDateTime,
            LastOccurrence = FixedDateTime
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Stack>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Tags);
        Assert.Equal(3, deserialized.Tags.Count);
        Assert.Contains("api", deserialized.Tags);
        Assert.Contains("auth", deserialized.Tags);
        Assert.Contains("critical", deserialized.Tags);
    }

    [Fact]
    public void Deserialize_StackWithFixedInfo_PreservesFixedInfo()
    {
        // Arrange
        var original = new Stack
        {
            Id = "stack-fixed",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Error,
            SignatureHash = "hash1",
            Status = StackStatus.Fixed,
            DateFixed = FixedDateTime,
            FixedInVersion = "2.1.0",
            FirstOccurrence = FixedDateTime.AddDays(-7),
            LastOccurrence = FixedDateTime
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Stack>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(StackStatus.Fixed, deserialized.Status);
        Assert.Equal("2.1.0", deserialized.FixedInVersion);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"ext-stack","organization_id":"ext-org","project_id":"ext-proj","type":"error","signature_hash":"exthash","title":"External Error","total_occurrences":100,"first_occurrence":"2024-01-01T00:00:00Z","last_occurrence":"2024-01-15T12:00:00Z","status":"regressed","tags":["external"]}""";

        // Act
        var stack = _serializer.Deserialize<Stack>(json);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal("ext-stack", stack.Id);
        Assert.Equal("External Error", stack.Title);
        Assert.Equal(100, stack.TotalOccurrences);
        Assert.Equal(StackStatus.Regressed, stack.Status);
    }
}
