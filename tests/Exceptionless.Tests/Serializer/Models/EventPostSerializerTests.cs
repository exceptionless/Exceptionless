using Exceptionless.Core.Queues.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public sealed class EventPostSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public EventPostSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void TrackingFlag_IsOmittedByDefaultAndRoundTripsWhenEnabled()
    {
        var untracked = CreateEventPost(processingCorrelationId: null, trackProcessing: false);
        string untrackedJson = _serializer.SerializeToString(untracked);

        Assert.DoesNotContain("\"track_processing\"", untrackedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"processing_correlation_id\"", untrackedJson, StringComparison.Ordinal);
        var deserializedUntracked = Assert.IsType<EventPost>(_serializer.Deserialize<EventPost>(untrackedJson));
        Assert.False(deserializedUntracked.TrackProcessing);
        Assert.Null(deserializedUntracked.ProcessingCorrelationId);

        var tracked = CreateEventPost(processingCorrelationId: null, trackProcessing: true);
        string trackedJson = _serializer.SerializeToString(tracked);

        Assert.Contains("\"track_processing\":true", trackedJson, StringComparison.Ordinal);
        Assert.True(Assert.IsType<EventPost>(_serializer.Deserialize<EventPost>(trackedJson)).TrackProcessing);

        var retry = CreateEventPost(processingCorrelationId: "tracked-post-123", trackProcessing: false);
        string retryJson = _serializer.SerializeToString(retry);
        Assert.Contains("\"processing_correlation_id\":\"tracked-post-123\"", retryJson, StringComparison.Ordinal);
        Assert.Equal("tracked-post-123", Assert.IsType<EventPost>(_serializer.Deserialize<EventPost>(retryJson)).ProcessingCorrelationId);
    }

    private static EventPost CreateEventPost(string? processingCorrelationId, bool trackProcessing) => new(false)
    {
        ApiVersion = 2,
        FilePath = "q/123/123.payload",
        OrganizationId = "537650f3b77efe23a47914f3",
        ProcessingCorrelationId = processingCorrelationId,
        ProjectId = "537650f3b77efe23a47914f4",
        TrackProcessing = trackProcessing,
    };
}
