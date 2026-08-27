using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Web.Assistant;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantUsageServiceTests
{
    [Fact]
    public async Task TryStartTurnAsync_SharedCache_EnforcesLimitAcrossServiceInstances()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions();
        var planOptions = CreatePlanOptions() with { MaximumTurnsPerMinute = 2 };
        var firstInstance = CreateService(cache, options, timeProvider);
        var secondInstance = CreateService(cache, options, timeProvider);

        Assert.True((await firstInstance.TryStartTurnAsync("organization-id", planOptions)).Allowed);
        Assert.True((await secondInstance.TryStartTurnAsync("organization-id", planOptions)).Allowed);

        var blocked = await firstInstance.TryStartTurnAsync("organization-id", planOptions);

        Assert.False(blocked.Allowed);
        Assert.Equal(AssistantUsageLimit.MinuteTurns, blocked.Limit);
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 12, 1, 0, TimeSpan.Zero), blocked.ResetAtUtc);
    }

    [Fact]
    public async Task TryStartTurnAsync_ConcurrentRequests_AtomicallyReservesConfiguredLimit()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions();
        var planOptions = CreatePlanOptions() with { MaximumTurnsPerMinute = 10 };
        var instances = new[] { CreateService(cache, options, timeProvider), CreateService(cache, options, timeProvider) };

        var decisions = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(index => instances[index % instances.Length].TryStartTurnAsync("organization-id", planOptions)));

        Assert.Equal(10, decisions.Count(decision => decision.Allowed));
        Assert.All(decisions.Where(decision => !decision.Allowed), decision => Assert.Equal(AssistantUsageLimit.MinuteTurns, decision.Limit));
        foreach (var decision in decisions)
            await decision.DisposeAsync();
    }

    [Fact]
    public async Task TryStartTurnAsync_DistinctLockProviders_EnforcesConcurrentLimitAcrossServiceInstances()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions();
        var planOptions = CreatePlanOptions() with { MaximumConcurrentTurns = 1 };
        var firstInstance = CreateService(cache, CreateLockProvider(cache, timeProvider), options, timeProvider);
        var secondInstance = CreateService(cache, CreateLockProvider(cache, timeProvider), options, timeProvider);

        var first = await firstInstance.TryStartTurnAsync("organization-id", planOptions);
        var blocked = await secondInstance.TryStartTurnAsync("organization-id", planOptions);

        Assert.True(first.Allowed);
        Assert.False(blocked.Allowed);
        Assert.Equal(AssistantUsageLimit.ConcurrentTurns, blocked.Limit);

        await first.DisposeAsync();
        await using var afterRelease = await secondInstance.TryStartTurnAsync("organization-id", planOptions);
        Assert.True(afterRelease.Allowed);
    }

    [Fact]
    public async Task TryStartTurnAsync_NewMinute_ResetsBurstLimit()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions();
        var planOptions = CreatePlanOptions() with { MaximumTurnsPerMinute = 1 };
        var service = CreateService(cache, options, timeProvider);

        Assert.True((await service.TryStartTurnAsync("organization-id", planOptions)).Allowed);
        Assert.False((await service.TryStartTurnAsync("organization-id", planOptions)).Allowed);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        Assert.True((await service.TryStartTurnAsync("organization-id", planOptions)).Allowed);
    }

    [Fact]
    public async Task RecordProviderUsageAsync_MultipleProviderRounds_AccumulatesMonthlyUsage()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var service = CreateService(cache, CreateOptions(), timeProvider);

        Assert.True((await service.TryStartTurnAsync("organization-id", CreatePlanOptions())).Allowed);
        await service.RecordProviderUsageAsync("organization-id", new AssistantProviderUsage(12_000, 1_000, 0.001234m));
        await service.RecordProviderUsageAsync("organization-id", new AssistantProviderUsage(8_000, 500, 0.000501m));

        var usage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(1, usage.Turns);
        Assert.Equal(20_000, usage.PromptTokens);
        Assert.Equal(1_500, usage.CompletionTokens);
        Assert.Equal(21_500, usage.TotalTokens);
        Assert.Equal(0.001735m, usage.CostUsd);
    }

    [Fact]
    public async Task RecordProviderUsageAsync_EmptyCache_SeedsDurableUsageBeforeIncrementing()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var organization = new Organization { Id = "organization-id", Name = "Test" };
        organization.AssistantUsage.Add(new AssistantUsageInfo
        {
            Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Turns = 4,
            PromptTokens = 900,
            CompletionTokens = 50,
            CostInMicrodollars = 900_000
        });
        var service = CreateService(cache, CreateOptions(), timeProvider, _ => Task.FromResult<Organization?>(organization));

        await service.RecordProviderUsageAsync("organization-id", new AssistantProviderUsage(100, 25, 0.10m));
        var usage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(4, usage.Turns);
        Assert.Equal(1_000, usage.PromptTokens);
        Assert.Equal(75, usage.CompletionTokens);
        Assert.Equal(1.00m, usage.CostUsd);
    }

    [Fact]
    public async Task GetMonthlyUsageAsync_SingleCounterMissing_RehydratesFromDurableUsage()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var organization = new Organization { Id = "organization-id", Name = "Test" };
        organization.AssistantUsage.Add(new AssistantUsageInfo
        {
            Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Turns = 4,
            PromptTokens = 900,
            CompletionTokens = 50,
            CostInMicrodollars = 900_000
        });
        var service = CreateService(cache, CreateOptions(), timeProvider, _ => Task.FromResult<Organization?>(organization));
        var initialUsage = await service.GetMonthlyUsageAsync("organization-id");
        using var scopedCache = new ScopedCacheClient(cache, "AssistantUsage");
        await scopedCache.RemoveAsync("organization:organization-id:usage:202608:cost-microdollars");

        var rehydratedUsage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(0.90m, initialUsage.CostUsd);
        Assert.Equal(initialUsage, rehydratedUsage);
    }

    [Fact]
    public async Task UsageActivity_IsForwardedToDurableOrganizationRecorder()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var recorder = new RecordingAssistantUsageRecorder();
        var service = CreateService(cache, CreateLockProvider(cache, timeProvider), CreateOptions(), timeProvider, recorder);

        await using var reservation = await service.TryStartTurnAsync("organization-id", CreatePlanOptions());
        await service.RecordProviderRequestStartedAsync("organization-id");
        await service.RecordProviderUsageAsync("organization-id", new AssistantProviderUsage(12_000, 750, 0.002345m), providerRequestAlreadyRecorded: true);
        await service.RecordToolCallsAsync("organization-id", 2);
        await service.RecordTurnCompletedAsync("organization-id");

        Assert.True(reservation.Allowed);
        Assert.Contains(recorder.Records, record => record.OrganizationId == "organization-id" && record.Increment.Turns == 1);
        Assert.Contains(recorder.Records, record => record.Increment.ProviderRequests == 1
            && record.Increment.PromptTokens == 0);
        Assert.Contains(recorder.Records, record => record.Increment.ProviderRequests == 0
            && record.Increment.PromptTokens == 12_000
            && record.Increment.CompletionTokens == 750
            && record.Increment.CostInMicrodollars == 2345);
        Assert.Contains(recorder.Records, record => record.Increment.ToolCalls == 2);
        Assert.Contains(recorder.Records, record => record.Increment.Completed == 1);
    }

    [Fact]
    public async Task StartProviderRequestAsync_WithoutUsage_PersistsConservativeMonthlyUsage()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var recorder = new RecordingAssistantUsageRecorder();
        var service = CreateService(cache, CreateLockProvider(cache, timeProvider), CreateOptions(), timeProvider, recorder);

        await using (var providerRequest = await service.StartProviderRequestAsync("organization-id", providerInputCharacters: 1_000))
        {
            providerRequest.MarkAccepted();
        }
        var usage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(1_000, usage.PromptTokens);
        Assert.Equal(AssistantLimits.MaximumOutputTokens, usage.CompletionTokens);
        Assert.True(usage.CostInMicrodollars > 0);
        Assert.Contains(recorder.Records, record => record.Increment.ProviderRequests == 1);
        var fallback = Assert.Single(recorder.Records, record => record.Increment.PromptTokens == 1_000);
        Assert.Equal(AssistantLimits.MaximumOutputTokens, fallback.Increment.CompletionTokens);
        Assert.Equal(usage.CostInMicrodollars, fallback.Increment.CostInMicrodollars);

        var organization = new Organization { Id = "organization-id", Name = "Test" };
        organization.AssistantUsage.Add(new AssistantUsageInfo
        {
            Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PromptTokens = fallback.Increment.PromptTokens,
            CompletionTokens = fallback.Increment.CompletionTokens,
            CostInMicrodollars = fallback.Increment.CostInMicrodollars
        });
        using var restartedCache = CreateCache(timeProvider);
        var restartedService = CreateService(restartedCache, CreateOptions(), timeProvider, _ => Task.FromResult<Organization?>(organization));

        var rehydratedUsage = await restartedService.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(usage.PromptTokens, rehydratedUsage.PromptTokens);
        Assert.Equal(usage.CompletionTokens, rehydratedUsage.CompletionTokens);
        Assert.Equal(usage.CostInMicrodollars, rehydratedUsage.CostInMicrodollars);
    }

    [Fact]
    public async Task StartProviderRequestAsync_NotAccepted_ReleasesReservationWithoutUsageFallback()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var recorder = new RecordingAssistantUsageRecorder();
        var service = CreateService(cache, CreateLockProvider(cache, timeProvider), CreateOptions(), timeProvider, recorder);

        await using (await service.StartProviderRequestAsync("organization-id", providerInputCharacters: 1_000))
        {
        }
        var usage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(0, usage.PromptTokens);
        Assert.Equal(0, usage.CompletionTokens);
        Assert.Equal(0, usage.CostInMicrodollars);
        Assert.Contains(recorder.Records, record => record.Increment.ProviderRequests == 1);
        Assert.DoesNotContain(recorder.Records, record => record.Increment.PromptTokens > 0 || record.Increment.CompletionTokens > 0);
    }

    [Fact]
    public async Task StartProviderRequestAsync_ReconcileAfterCounterEviction_RestoresDurableBaseline()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var organization = new Organization { Id = "organization-id", Name = "Test" };
        organization.AssistantUsage.Add(new AssistantUsageInfo
        {
            Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PromptTokens = 900,
            CompletionTokens = 50,
            CostInMicrodollars = 900_000
        });
        var service = CreateService(cache, CreateOptions(), timeProvider, _ => Task.FromResult<Organization?>(organization));
        var providerRequest = await service.StartProviderRequestAsync("organization-id", providerInputCharacters: 1_000);
        using var scopedCache = new ScopedCacheClient(cache, "AssistantUsage");
        await scopedCache.RemoveAsync("organization:organization-id:usage:202608:prompt-tokens");

        await providerRequest.ReconcileAsync(new AssistantProviderUsage(100, 25, 0.10m));
        var usage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(1_000, usage.PromptTokens);
        Assert.Equal(75, usage.CompletionTokens);
        Assert.Equal(1.00m, usage.CostUsd);
    }

    [Fact]
    public async Task StartProviderRequestAsync_WithActualUsage_ReplacesEstimateWithoutFallback()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var recorder = new RecordingAssistantUsageRecorder();
        var service = CreateService(cache, CreateLockProvider(cache, timeProvider), CreateOptions(), timeProvider, recorder);

        await using (var providerRequest = await service.StartProviderRequestAsync("organization-id", providerInputCharacters: 1_000))
            await providerRequest.ReconcileAsync(new AssistantProviderUsage(250, 50, 0.001m));
        var usage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(250, usage.PromptTokens);
        Assert.Equal(50, usage.CompletionTokens);
        Assert.Equal(0.001m, usage.CostUsd);
        Assert.Single(recorder.Records, record => record.Increment.PromptTokens > 0);
        Assert.Contains(recorder.Records, record => record.Increment.PromptTokens == 250
            && record.Increment.CompletionTokens == 50
            && record.Increment.CostInMicrodollars == 1_000);
    }

    [Fact]
    public async Task StartProviderRequestAsync_MonthBoundary_ReconcilesOriginalMonthReservation()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 31, 23, 59, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var recorder = new RecordingAssistantUsageRecorder();
        var service = CreateService(cache, CreateLockProvider(cache, timeProvider), CreateOptions(), timeProvider, recorder);
        var providerRequest = await service.StartProviderRequestAsync("organization-id", providerInputCharacters: 1_000);

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        using var scopedCache = new ScopedCacheClient(cache, "AssistantUsage");
        await scopedCache.RemoveAllAsync(
        [
            "organization:organization-id:turns:month:202608",
            "organization:organization-id:usage:202608:prompt-tokens",
            "organization:organization-id:usage:202608:completion-tokens",
            "organization:organization-id:usage:202608:cost-microdollars"
        ]);
        await providerRequest.ReconcileAsync(new AssistantProviderUsage(2_000, 500, 0.01m));

        var septemberUsage = await service.GetMonthlyUsageAsync("organization-id");
        long augustPromptTokens = await scopedCache.GetAsync<long>("organization:organization-id:usage:202608:prompt-tokens", 0);
        long augustReservedTokens = await scopedCache.GetAsync<long>("organization:organization-id:usage:202608:reserved-prompt-tokens", 0);
        long septemberReservedTokens = await scopedCache.GetAsync<long>("organization:organization-id:usage:202609:reserved-prompt-tokens", 0);

        Assert.Equal(2_000, augustPromptTokens);
        Assert.Equal(0, augustReservedTokens);
        Assert.Equal(0, septemberUsage.TotalTokens);
        Assert.Equal(0, septemberReservedTokens);
        var durableUsage = Assert.Single(recorder.Records, record => record.Increment.PromptTokens == 2_000);
        Assert.Equal(new DateTime(2026, 8, 31, 23, 59, 0, DateTimeKind.Utc), durableUsage.Increment.ProviderUsageDateUtc);
    }

    [Fact]
    public async Task StartProviderRequestAsync_DurableReconciliationFails_PropagatesAndPreservesReservation()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var recorder = new RecordingAssistantUsageRecorder();
        var service = CreateService(cache, CreateLockProvider(cache, timeProvider), CreateOptions(), timeProvider, recorder);
        var providerRequest = await service.StartProviderRequestAsync("organization-id", providerInputCharacters: 1_000);
        providerRequest.MarkAccepted();
        recorder.Exception = new InvalidOperationException("durable usage unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => providerRequest.ReconcileAsync(new AssistantProviderUsage(250, 50, 0.001m)));
        var reservedUsage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(1_000, reservedUsage.PromptTokens);
        Assert.Equal(AssistantLimits.MaximumOutputTokens, reservedUsage.CompletionTokens);

        recorder.Exception = null;
        await providerRequest.DisposeAsync();
        var reconciledUsage = await service.GetMonthlyUsageAsync("organization-id");

        Assert.Equal(reservedUsage, reconciledUsage);
        Assert.Single(recorder.Records, record => record.Increment.PromptTokens == 1_000);
    }

    [Fact]
    public async Task TryStartTurnAsync_MonthlyCostReached_BlocksBeforeProviderCall()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions();
        var planOptions = CreatePlanOptions() with { MaximumMonthlyCostUsd = 1.25m };
        var service = CreateService(cache, options, timeProvider);
        await service.RecordProviderUsageAsync("organization-id", new AssistantProviderUsage(1, 1, 1.25m));

        var blocked = await service.TryStartTurnAsync("organization-id", planOptions);

        Assert.False(blocked.Allowed);
        Assert.Equal(AssistantUsageLimit.MonthlyCost, blocked.Limit);
    }

    [Fact]
    public async Task TryStartTurnAsync_MonthlyTokenLimitReached_BlocksBeforeProviderCall()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions();
        var planOptions = CreatePlanOptions() with { MaximumMonthlyTokens = 1_000_000 };
        var service = CreateService(cache, options, timeProvider);
        await service.RecordProviderUsageAsync("organization-id", new AssistantProviderUsage(900_000, 100_000, 0));

        var blocked = await service.TryStartTurnAsync("organization-id", planOptions);

        Assert.False(blocked.Allowed);
        Assert.Equal(AssistantUsageLimit.MonthlyTokens, blocked.Limit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryContinueTurnAsync_MonthlySpendGuard_RechecksBetweenProviderRounds(bool useCostLimit)
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var planOptions = CreatePlanOptions() with
        {
            MaximumMonthlyCostUsd = useCostLimit ? 0.50m : 5m,
            MaximumMonthlyTokens = useCostLimit ? 25_000_000 : 100
        };
        var service = CreateService(cache, CreateOptions(), timeProvider);
        await service.RecordProviderUsageAsync(
            "organization-id",
            new AssistantProviderUsage(100, 25, useCostLimit ? 0.50m : 0));

        var decision = await service.TryContinueTurnAsync("organization-id", planOptions);

        Assert.False(decision.Allowed);
        Assert.Equal(useCostLimit ? AssistantUsageLimit.MonthlyCost : AssistantUsageLimit.MonthlyTokens, decision.Limit);
    }

    [Fact]
    public async Task TryStartTurnAsync_Development_DoesNotEnforceOrganizationLimit()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        using var cache = CreateCache(timeProvider);
        var options = CreateOptions(new Dictionary<string, string?>
        {
            ["AppMode"] = AppMode.Development.ToString()
        });
        var service = CreateService(cache, options, timeProvider);

        Assert.True((await service.TryStartTurnAsync(null, planOptions: null)).Allowed);
        Assert.True((await service.TryStartTurnAsync(null, planOptions: null)).Allowed);
    }

    private static InMemoryCacheClient CreateCache(TimeProvider timeProvider) => new(new InMemoryCacheClientOptions
    {
        LoggerFactory = NullLoggerFactory.Instance,
        TimeProvider = timeProvider
    });

    private static AssistantUsageService CreateService(ICacheClient cache, AppOptions options, TimeProvider timeProvider)
        => CreateService(cache, CreateLockProvider(cache, timeProvider), options, timeProvider);

    private static AssistantUsageService CreateService(ICacheClient cache, ILockProvider lockProvider, AppOptions options, TimeProvider timeProvider)
        => CreateService(cache, lockProvider, options, timeProvider, new RecordingAssistantUsageRecorder());

    private static AssistantUsageService CreateService(ICacheClient cache, ILockProvider lockProvider, AppOptions options, TimeProvider timeProvider, IAssistantUsageRecorder usageRecorder)
        => new(cache, lockProvider, usageRecorder, options, timeProvider, NullLogger<AssistantUsageService>.Instance);

    private static AssistantUsageService CreateService(
        ICacheClient cache,
        AppOptions options,
        TimeProvider timeProvider,
        Func<string, Task<Organization?>> loadOrganizationAsync)
        => new(
            cache,
            CreateLockProvider(cache, timeProvider),
            new RecordingAssistantUsageRecorder(),
            options,
            timeProvider,
            NullLogger<AssistantUsageService>.Instance,
            loadOrganizationAsync);

    private static ILockProvider CreateLockProvider(ICacheClient cache, TimeProvider timeProvider)
    {
        var resiliencePolicyProvider = new ResiliencePolicyProvider();
        var serializer = new SystemTextJsonSerializer(new JsonSerializerOptions().ConfigureExceptionlessDefaults());
        var messageBus = new InMemoryMessageBus(new InMemoryMessageBusOptions
        {
            Serializer = serializer,
            TimeProvider = timeProvider,
            ResiliencePolicyProvider = resiliencePolicyProvider,
            LoggerFactory = NullLoggerFactory.Instance
        });
        return new CacheLockProvider(cache, messageBus, timeProvider, resiliencePolicyProvider, NullLoggerFactory.Instance);
    }

    private static AssistantPlanOptions CreatePlanOptions() => new()
    {
        MaximumConcurrentTurns = 100,
        MaximumTurnsPerMinute = 10,
        MaximumMonthlyTokens = 25_000_000,
        MaximumMonthlyCostUsd = 5m
    };

    private static AppOptions CreateOptions(Dictionary<string, string?>? values = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["AppMode"] = AppMode.Production.ToString(),
            ["BaseURL"] = "https://localhost"
        };

        if (values is not null)
        {
            foreach (var pair in values)
                configurationValues[pair.Key] = pair.Value;
        }

        return AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build());
    }
}
