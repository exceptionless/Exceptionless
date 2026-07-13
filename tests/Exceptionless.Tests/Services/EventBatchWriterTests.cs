using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventBatchWriterTests
{
    [Fact]
    public void GetDeterministicEventId_SameInputs_ReturnsStableObjectId()
    {
        var date = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        string first = EventBatchWriter.GetDeterministicEventId("507f1f77bcf86cd799439011", "01J123456789ABCDEFGHJKLMNP", date);
        string second = EventBatchWriter.GetDeterministicEventId("507f1f77bcf86cd799439011", "01J123456789ABCDEFGHJKLMNP", date);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{24}$", first);
    }

    [Fact]
    public void GetDeterministicEventId_DifferentProject_ProducesDifferentId()
    {
        var date = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        string first = EventBatchWriter.GetDeterministicEventId("507f1f77bcf86cd799439011", "event-1", date);
        string second = EventBatchWriter.GetDeterministicEventId("507f191e810c19729de860ea", "event-1", date);

        Assert.NotEqual(first, second);
    }
}
