using Exceptionless.Core;
using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class IngestionStackUsageStoreTests : TestWithServices
{
    private readonly IIngestionStackUsageStore _store;

    public IngestionStackUsageStoreTests(ITestOutputHelper output) : base(output)
    {
        _store = GetService<IIngestionStackUsageStore>();
    }

    [Fact]
    public async Task SettleAsync_CallerFailsAfterSettlement_RetryDoesNotDoubleCount()
    {
        var usage = CreateUsage("event-a", "stack-a", new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var settled = await _store.SettleAsync([usage], TestCancellationToken);
            Assert.Equal(1, Assert.Single(settled).Count);
            throw new InvalidOperationException("failure after the atomic settlement");
        });

        var retry = await _store.SettleAsync([usage], TestCancellationToken);
        var pending = await _store.ClaimPendingAsync(10, TestCancellationToken);
        var completed = await GetService<IngestionSideEffectExecutor>().GetCompletedIdentitiesAsync(
            IngestionSideEffectExecutor.StatisticsStage,
            usage.ProjectId,
            [usage.EventId]);

        Assert.Empty(retry);
        Assert.Equal(1, Assert.Single(pending).Count);
        Assert.Contains(usage.EventId, completed);
    }

    [Fact]
    public async Task SettleAsync_OverlappingBatches_CountsEachEventOnce()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        var eventA = CreateUsage("event-a", "stack-a", occurrence);
        var eventB = CreateUsage("event-b", "stack-a", occurrence.AddSeconds(1));
        var eventC = CreateUsage("event-c", "stack-a", occurrence.AddSeconds(2));

        var first = await _store.SettleAsync([eventA, eventB], TestCancellationToken);
        var second = await _store.SettleAsync([eventB, eventC], TestCancellationToken);
        var pending = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));

        Assert.Equal(2, Assert.Single(first).Count);
        Assert.Equal(1, Assert.Single(second).Count);
        Assert.Equal(3, pending.Count);
        Assert.Equal(eventA.OccurrenceDateUtc, pending.MinimumOccurrenceDateUtc);
        Assert.Equal(eventC.OccurrenceDateUtc, pending.MaximumOccurrenceDateUtc);
    }

    [Fact]
    public async Task SettleAsync_ConcurrentOverlappingBatches_CountsEachEventOnce()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        var eventA = CreateUsage("event-a", "stack-a", occurrence);
        var eventB = CreateUsage("event-b", "stack-a", occurrence.AddSeconds(1));
        var eventC = CreateUsage("event-c", "stack-a", occurrence.AddSeconds(2));

        Task<IReadOnlyCollection<StackUsageSummary>>[] calls = Enumerable.Range(0, 40)
            .Select(index => _store.SettleAsync(
                index % 2 == 0 ? [eventA, eventB] : [eventB, eventC],
                TestCancellationToken))
            .ToArray();
        var settled = await Task.WhenAll(calls);
        var pending = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));

        Assert.Equal(3, settled.SelectMany(result => result).Sum(result => result.Count));
        Assert.Equal(3, pending.Count);
    }

    [Fact]
    public async Task SettleAsync_MultipleStacks_SettlesWholeBatch()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

        var settled = await _store.SettleAsync(
        [
            CreateUsage("event-a", "stack-a", occurrence),
            CreateUsage("event-b", "stack-b", occurrence.AddSeconds(1)),
            CreateUsage("event-c", "stack-a", occurrence.AddSeconds(2))
        ], TestCancellationToken);

        Assert.Equal(2, settled.Count);
        Assert.Equal(3, settled.Sum(result => result.Count));
    }

    [Fact]
    public async Task ClaimPendingAsync_BusyFirstProject_DoesNotStarveLaterProject()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await _store.SettleAsync(
            Enumerable.Range(0, 6)
                .Select(index => CreateUsage(
                    $"event-a-{index}",
                    $"stack-a-{index}",
                    occurrence.AddSeconds(index),
                    "project-a"))
                .ToArray(),
            TestCancellationToken);
        await _store.SettleAsync(
            [CreateUsage("event-b", "stack-b", occurrence, "project-b")],
            TestCancellationToken);

        var pending = await _store.ClaimPendingAsync(2, TestCancellationToken);

        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, usage => usage.ProjectId == "project-a");
        Assert.Contains(pending, usage => usage.ProjectId == "project-b");
    }

    [Fact]
    public async Task AcknowledgeAsync_NewUsageBehindClaim_RemainsPending()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await _store.SettleAsync([CreateUsage("event-a", "stack-a", occurrence)], TestCancellationToken);
        var taken = await _store.ClaimPendingAsync(10, TestCancellationToken);
        await _store.SettleAsync([CreateUsage("event-b", "stack-a", occurrence.AddSeconds(1))], TestCancellationToken);

        await _store.AcknowledgeAsync(taken, TestCancellationToken);
        var pending = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));

        Assert.Equal(1, pending.Count);
        Assert.Equal(occurrence.AddSeconds(1), pending.MinimumOccurrenceDateUtc);
        Assert.Equal(occurrence.AddSeconds(1), pending.MaximumOccurrenceDateUtc);
    }

    [Fact]
    public async Task ClaimPendingAsync_CrashBeforeAcknowledge_ReusesSettlementIdentity()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await _store.SettleAsync([CreateUsage("event-a", "stack-a", occurrence)], TestCancellationToken);

        var first = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));
        Assert.Empty(await _store.ClaimPendingAsync(10, TestCancellationToken));

        TimeProvider.Advance(GetService<AppOptions>().EventIngestionV3.StackUsageClaimLease.Add(TimeSpan.FromSeconds(1)));
        var recovered = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));

        Assert.Equal(first.SettlementSequence, recovered.SettlementSequence);
        Assert.Equal(first.Count, recovered.Count);
        Assert.Equal(first.MinimumOccurrenceDateUtc, recovered.MinimumOccurrenceDateUtc);
        Assert.Equal(first.MaximumOccurrenceDateUtc, recovered.MaximumOccurrenceDateUtc);
    }

    [Fact]
    public async Task ClaimPendingAsync_ConcurrentDrainers_ClaimEachSettlementOnce()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 20; index++)
        {
            await _store.SettleAsync(
                [CreateUsage($"event-{index}", $"stack-{index}", occurrence.AddSeconds(index), $"project-{index}")],
                TestCancellationToken);
        }

        Task<IReadOnlyCollection<StackUsageClaim>>[] drainers = Enumerable.Range(0, 20)
            .Select(_ => _store.ClaimPendingAsync(1, TestCancellationToken))
            .ToArray();
        StackUsageClaim[] claims = (await Task.WhenAll(drainers)).SelectMany(result => result).ToArray();

        Assert.Equal(20, claims.Length);
        Assert.Equal(20, claims.Select(claim => (claim.ProjectId, claim.StackId, claim.SettlementSequence)).Distinct().Count());
    }

    [Fact]
    public async Task AcknowledgeAsync_StaleClaim_CannotRemoveNewerSettlement()
    {
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await _store.SettleAsync([CreateUsage("event-a", "stack-a", occurrence)], TestCancellationToken);
        var first = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));
        await _store.SettleAsync([CreateUsage("event-b", "stack-a", occurrence.AddSeconds(1))], TestCancellationToken);
        await _store.AcknowledgeAsync([first], TestCancellationToken);
        var second = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));

        await _store.AcknowledgeAsync([first], TestCancellationToken);
        Assert.Empty(await _store.ClaimPendingAsync(10, TestCancellationToken));
        TimeProvider.Advance(GetService<AppOptions>().EventIngestionV3.StackUsageClaimLease.Add(TimeSpan.FromSeconds(1)));
        var recovered = Assert.Single(await _store.ClaimPendingAsync(10, TestCancellationToken));

        Assert.Equal(second.SettlementSequence, recovered.SettlementSequence);
        Assert.True(second.SettlementSequence > first.SettlementSequence);
    }

    private static IngestionStackUsage CreateUsage(
        string eventId,
        string stackId,
        DateTime occurrenceDateUtc,
        string projectId = "project") =>
        new(eventId, "organization", projectId, stackId, occurrenceDateUtc);
}
