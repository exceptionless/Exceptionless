using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventIngestionIdStoreTests : TestWithServices
{
    private const string ProjectId = "507f1f77bcf86cd799439011";
    private readonly IEventIngestionIdStore _store;

    public EventIngestionIdStoreTests(ITestOutputHelper output) : base(output)
    {
        _store = GetService<IEventIngestionIdStore>();
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentClaims_ReturnsOneStableIdentity()
    {
        const string clientId = "concurrent-client-id";
        var first = CreateCandidate(clientId, new DateTimeOffset(2026, 7, 13, 23, 59, 59, TimeSpan.Zero));
        var second = CreateCandidate(clientId, new DateTimeOffset(2026, 7, 14, 0, 0, 1, TimeSpan.Zero));

        Task<IReadOnlyDictionary<string, EventIngestionId>> firstClaim = _store.GetOrAddAsync(
            ProjectId,
            [first],
            TimeSpan.FromDays(7),
            TestCancellationToken);
        Task<IReadOnlyDictionary<string, EventIngestionId>> secondClaim = _store.GetOrAddAsync(
            ProjectId,
            [second],
            TimeSpan.FromDays(7),
            TestCancellationToken);

        EventIngestionId firstResult = (await firstClaim)[clientId];
        EventIngestionId secondResult = (await secondClaim)[clientId];
        Assert.Equal(firstResult, secondResult);
        Assert.Contains(firstResult, new[] { first.Identity, second.Identity });
    }

    [Fact]
    public async Task GetAsync_UnknownClientId_DoesNotPreclaimIdentity()
    {
        const string clientId = "status-polled-before-ingestion";
        var candidate = CreateCandidate(clientId, new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));

        var before = await _store.GetAsync(ProjectId, [clientId], TestCancellationToken);
        var claimed = await _store.GetOrAddAsync(ProjectId, [candidate], TimeSpan.FromDays(7), TestCancellationToken);

        Assert.Empty(before);
        Assert.Equal(candidate.Identity, claimed[clientId]);
    }

    [Fact]
    public async Task GetOrAddAsync_AfterExpiration_AllowsNewDateRoutableIdentity()
    {
        const string clientId = "expired-client-id";
        var first = CreateCandidate(clientId, new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));
        var second = CreateCandidate(clientId, new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero));
        TimeProvider.SetUtcNow(first.Identity.EventDate);

        EventIngestionId firstResult = (await _store.GetOrAddAsync(
            ProjectId,
            [first],
            TimeSpan.FromDays(7),
            TestCancellationToken))[clientId];
        TimeProvider.Advance(TimeSpan.FromDays(8));
        EventIngestionId secondResult = (await _store.GetOrAddAsync(
            ProjectId,
            [second],
            TimeSpan.FromDays(7),
            TestCancellationToken))[clientId];

        Assert.Equal(first.Identity, firstResult);
        Assert.Equal(second.Identity, secondResult);
        Assert.NotEqual(firstResult.EventId, secondResult.EventId);
    }

    private static EventIngestionIdCandidate CreateCandidate(string clientId, DateTimeOffset eventDate)
    {
        var identity = new EventIngestionId(
            EventBatchWriter.GetDeterministicEventId(ProjectId, clientId, eventDate.UtcDateTime),
            eventDate,
            eventDate.UtcDateTime);
        return new EventIngestionIdCandidate(clientId, identity);
    }
}
