using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed partial class UsageServiceTests
{
    [Fact]
    public async Task ReserveEventsAsync_ConcurrentCallers_DoNotOverReserve()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Concurrent reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int available = await _usageService.GetEventsLeftAsync(organization.Id);

        int[] reservations = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(async _ => (await _usageService.ReserveEventsAsync(organization.Id, available)).Count));

        Assert.Equal(available, reservations.Sum());
        var activeReservations = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _usageService.ReserveEventsAsync(organization.Id, available)));
        Assert.All(activeReservations, reservation => Assert.Equal(0, reservation.Count));

        TimeProvider.Advance(TimeSpan.FromMinutes(11));
        var afterExpiry = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, afterExpiry.Count);
        await _usageService.ReleaseEventReservationAsync(afterExpiry);
    }

    [Fact]
    public async Task ReserveEventsAsync_BucketRollsBeforeLeaseExpires_DoesNotReuseCapacity()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Bucket rollover reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int available = await _usageService.GetEventsLeftAsync(organization.Id);
        var first = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, first.Count);

        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var nextBucket = await _usageService.ReserveEventsAsync(organization.Id, available);

        Assert.Equal(0, nextBucket.Count);
        await _usageService.ReleaseEventReservationAsync(first);
        var afterRelease = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, afterRelease.Count);
        await _usageService.ReleaseEventReservationAsync(afterRelease);
    }

    [Fact]
    public async Task ReserveEventsAsync_PlanLimitIncreases_PreservesOutstandingCapacity()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Plan change reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int originalAvailable = await _usageService.GetEventsLeftAsync(organization.Id);
        var first = await _usageService.ReserveEventsAsync(organization.Id, originalAvailable);
        Assert.Equal(750, first.Count);

        organization.MaxEventsPerMonth = 1000;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());
        await GetService<ICacheClient>().RemoveAsync($"usage:limits:{organization.Id}");
        int increasedAvailable = await _usageService.GetEventsLeftAsync(organization.Id);
        var afterPlanIncrease = await _usageService.ReserveEventsAsync(organization.Id, increasedAvailable);

        Assert.Equal(1000, increasedAvailable);
        Assert.Equal(250, afterPlanIncrease.Count);
        await _usageService.ReleaseEventReservationAsync(first);
        await _usageService.ReleaseEventReservationAsync(afterPlanIncrease);
    }

    [Fact]
    public async Task ReserveEventsAsync_PartialCapacity_IsAdmittedDeterministically()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Partial reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int available = await _usageService.GetEventsLeftAsync(organization.Id);

        var first = await _usageService.ReserveEventsAsync(organization.Id, available - 1);
        var second = await _usageService.ReserveEventsAsync(organization.Id, 5);

        Assert.Equal(available - 1, first.Count);
        Assert.Equal(1, second.Count);
        await _usageService.ReleaseEventReservationAsync(first);
        await _usageService.ReleaseEventReservationAsync(second);

        // Releasing an already released lease is a no-op and cannot create negative capacity.
        await _usageService.ReleaseEventReservationAsync(second);
        var third = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, third.Count);
        await _usageService.ReleaseEventReservationAsync(third);
    }

    [Fact]
    public async Task IncrementTotalAsync_WriterOwnedV3Settlements_CountsDistinctEventsInCommit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Idempotent settlement",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Idempotent settlement",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        DateTime createdUtc = TimeProvider.GetUtcNow().UtcDateTime;
        EventUsageSettlement[] settlements =
        [
            new("event-1", createdUtc),
            new("event-2", createdUtc),
            new("event-1", createdUtc)
        ];

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, settlements);

        UsageInfoResponse organizationUsage = await _usageService.GetUsageAsync(organization.Id);
        UsageInfoResponse projectUsage = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(2, organizationUsage.CurrentUsage.Total);
        Assert.Equal(2, projectUsage.CurrentUsage.Total);
    }

    [Fact]
    public async Task IncrementTotalAsync_LateWriterOwnedSettlementAfterBucketSave_MovesToCurrentBucket()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Late settlement",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Late settlement",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        DateTime originalBucketUtc = TimeProvider.GetUtcNow().UtcDateTime;
        var first = new EventUsageSettlement("event-1", originalBucketUtc);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [first]);
        TimeProvider.Advance(TimeSpan.FromMinutes(11));
        await _usageService.SavePendingUsageAsync();

        await _usageService.IncrementTotalAsync(organization.Id, project.Id,
            [new EventUsageSettlement("event-2", originalBucketUtc)]);

        UsageInfoResponse organizationUsage = await _usageService.GetUsageAsync(organization.Id);
        UsageInfoResponse projectUsage = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(2, organizationUsage.CurrentUsage.Total);
        Assert.Equal(2, projectUsage.CurrentUsage.Total);
    }

    [Fact]
    public async Task IncrementTotalAsync_SettlementPastSafetyWindow_FailsOpen()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Expired idempotency settlement",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Expired idempotency settlement",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        DateTime createdUtc = TimeProvider.GetUtcNow().UtcDateTime;
        var settlement = new EventUsageSettlement("event-expired", createdUtc);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [settlement]);
        TimeProvider.Advance(TimeSpan.FromMinutes(11));
        await _usageService.SavePendingUsageAsync();

        TimeSpan idempotencyWindow = GetService<AppOptions>().EventIngestionV3.IdempotencyWindow;
        TimeProvider.Advance(idempotencyWindow.Subtract(TimeSpan.FromMinutes(11)).Add(TimeSpan.FromSeconds(1)));
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [settlement]);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id)).CurrentUsage.Total);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id, project.Id)).CurrentUsage.Total);

        // The durable event age still prevents reconstruction of an old, already-closed bucket
        // after the processed marker expires.
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [settlement]);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id)).CurrentUsage.Total);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id, project.Id)).CurrentUsage.Total);
    }
}
