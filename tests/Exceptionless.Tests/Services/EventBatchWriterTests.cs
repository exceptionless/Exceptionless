using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventBatchWriterTests
{
    [Fact]
    public void GetDeterministicEventId_SameInputs_ReturnsStableDateRoutableObjectId()
    {
        DateTime eventDateUtc = new(2026, 7, 13, 12, 34, 56, DateTimeKind.Utc);
        string first = EventBatchWriter.GetDeterministicEventId("507f1f77bcf86cd799439011", "01J123456789ABCDEFGHJKLMNP", eventDateUtc);
        string second = EventBatchWriter.GetDeterministicEventId("507f1f77bcf86cd799439011", "01J123456789ABCDEFGHJKLMNP", eventDateUtc);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{24}$", first);
        Assert.Equal(new DateTimeOffset(eventDateUtc).ToUnixTimeSeconds(), Convert.ToUInt32(first[..8], 16));
    }

    [Fact]
    public void GetDeterministicEventId_DifferentProject_ProducesDifferentId()
    {
        DateTime eventDateUtc = new(2026, 7, 13, 12, 34, 56, DateTimeKind.Utc);
        string first = EventBatchWriter.GetDeterministicEventId("507f1f77bcf86cd799439011", "event-1", eventDateUtc);
        string second = EventBatchWriter.GetDeterministicEventId("507f191e810c19729de860ea", "event-1", eventDateUtc);

        Assert.NotEqual(first, second);
    }
}
