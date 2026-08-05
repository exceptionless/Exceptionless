using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantUsageService(
    ICacheClient cacheClient,
    ILockProvider lockProvider,
    IAssistantUsageRecorder usageRecorder,
    AppOptions appOptions,
    TimeProvider timeProvider,
    ILogger<AssistantUsageService> logger,
    IOrganizationRepository? organizationRepository = null)
{
    private const long MicrodollarsPerDollar = 1_000_000;
    private readonly ScopedCacheClient _cache = new(cacheClient, "AssistantUsage");

    public async Task<AssistantTurnReservation> TryStartTurnAsync(string? organizationId, AssistantPlanOptions? planOptions)
    {
        if (appOptions.AppMode == AppMode.Development)
            return AssistantTurnReservation.CreateAllowed();

        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentNullException.ThrowIfNull(planOptions);

        var now = timeProvider.GetUtcNow();
        var month = GetMonthWindow(now);
        var monthlyUsage = await GetMonthlyUsageAsync(organizationId, now);

        if (monthlyUsage.CostInMicrodollars >= ToMicrodollars(planOptions.MaximumMonthlyCostUsd))
        {
            await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { BlockedByCostLimit = 1 });
            return AssistantTurnReservation.Blocked(AssistantUsageDecision.Blocked(
                AssistantUsageLimit.MonthlyCost,
                month.ResetAtUtc,
                "This organization has reached Exie's monthly AI cost limit."));
        }

        if (monthlyUsage.TotalTokens >= planOptions.MaximumMonthlyTokens)
        {
            await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { BlockedByTokenLimit = 1 });
            return AssistantTurnReservation.Blocked(AssistantUsageDecision.Blocked(
                AssistantUsageLimit.MonthlyTokens,
                month.ResetAtUtc,
                "This organization has reached Exie's monthly AI token limit."));
        }

        var leaseDuration = TimeSpan.FromSeconds(Math.Max(30, AssistantLimits.MaximumTurnDurationSeconds + 30));
        ILock? lease = null;
        for (int slot = 0; slot < planOptions.MaximumConcurrentTurns; slot++)
        {
            lease = await lockProvider.TryAcquireAsync(
                $"assistant-turn:{organizationId}:{slot}",
                leaseDuration,
                TimeSpan.Zero);
            if (lease is not null)
                break;
        }

        if (lease is null)
        {
            await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { BlockedByConcurrency = 1 });
            return AssistantTurnReservation.Blocked(AssistantUsageDecision.Blocked(
                AssistantUsageLimit.ConcurrentTurns,
                now.AddSeconds(5),
                "This organization already has the maximum number of Exie responses in progress."));
        }

        var minute = GetMinuteWindow(now);
        var decision = await TryReserveWindowAsync(
            organizationId,
            "minute",
            minute,
            planOptions.MaximumTurnsPerMinute,
            AssistantUsageLimit.MinuteTurns,
            "Exie is being used too quickly for this organization. Try again in a moment.");
        if (decision is not null)
        {
            await lease.DisposeAsync();
            await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { BlockedByRateLimit = 1 });
            return AssistantTurnReservation.Blocked(decision);
        }

        await _cache.IncrementAsync(GetTurnKey(organizationId, "month", month.Id), 1, month.ResetAtUtc.UtcDateTime);
        await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { Turns = 1 });

        return AssistantTurnReservation.CreateAllowed(lease);
    }

    public async Task<AssistantUsageDecision> TryContinueTurnAsync(string? organizationId, AssistantPlanOptions planOptions)
    {
        if (appOptions.AppMode == AppMode.Development)
            return AssistantUsageDecision.AllowedDecision;

        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        var now = timeProvider.GetUtcNow();
        var month = GetMonthWindow(now);
        var monthlyUsage = await GetMonthlyUsageAsync(organizationId, now);
        if (monthlyUsage.CostInMicrodollars >= ToMicrodollars(planOptions.MaximumMonthlyCostUsd))
        {
            await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { BlockedByCostLimit = 1 });
            return AssistantUsageDecision.Blocked(
                AssistantUsageLimit.MonthlyCost,
                month.ResetAtUtc,
                "This organization reached Exie's monthly AI cost limit while completing the response.");
        }

        if (monthlyUsage.TotalTokens >= planOptions.MaximumMonthlyTokens)
        {
            await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { BlockedByTokenLimit = 1 });
            return AssistantUsageDecision.Blocked(
                AssistantUsageLimit.MonthlyTokens,
                month.ResetAtUtc,
                "This organization reached Exie's monthly AI token limit while completing the response.");
        }

        return AssistantUsageDecision.AllowedDecision;
    }

    public Task RecordProviderRequestStartedAsync(string? organizationId)
        => TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { ProviderRequests = 1 });

    public async Task RecordProviderUsageAsync(string? organizationId, AssistantProviderUsage usage, bool providerRequestAlreadyRecorded = false)
    {
        if (String.IsNullOrWhiteSpace(organizationId))
            return;

        var month = GetMonthWindow(timeProvider.GetUtcNow());
        var tasks = new List<Task>
        {
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "prompt-tokens"), Math.Max(0, usage.PromptTokens), month.ResetAtUtc.UtcDateTime),
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "completion-tokens"), Math.Max(0, usage.CompletionTokens), month.ResetAtUtc.UtcDateTime)
        };

        long costInMicrodollars = ToMicrodollars(usage.CostUsd);
        if (costInMicrodollars > 0)
            tasks.Add(_cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "cost-microdollars"), costInMicrodollars, month.ResetAtUtc.UtcDateTime));

        await Task.WhenAll(tasks);
        await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement
        {
            ProviderRequests = providerRequestAlreadyRecorded ? 0 : 1,
            PromptTokens = Math.Max(0, usage.PromptTokens),
            CompletionTokens = Math.Max(0, usage.CompletionTokens),
            CostInMicrodollars = costInMicrodollars
        });
    }

    public Task RecordToolCallsAsync(string? organizationId, int count)
        => TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { ToolCalls = Math.Max(0, count) });

    public Task RecordTurnCompletedAsync(string? organizationId)
        => TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { Completed = 1 });

    public Task RecordTurnFailedAsync(string? organizationId)
        => TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { Failed = 1 });

    public Task RecordTurnCancelledAsync(string? organizationId)
        => TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { Cancelled = 1 });

    public Task<AssistantMonthlyUsage> GetMonthlyUsageAsync(string organizationId)
        => GetMonthlyUsageAsync(organizationId, timeProvider.GetUtcNow());

    private async Task<AssistantMonthlyUsage> GetMonthlyUsageAsync(string organizationId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);

        var month = GetMonthWindow(now);
        long turns = await _cache.GetAsync<long>(GetTurnKey(organizationId, "month", month.Id), 0);
        long promptTokens = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "prompt-tokens"), 0);
        long completionTokens = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "completion-tokens"), 0);
        long costInMicrodollars = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "cost-microdollars"), 0);
        if (organizationRepository is not null)
        {
            try
            {
                var organization = await organizationRepository.GetByIdAsync(organizationId, options => options.Cache());
                var durableUsage = organization?.AssistantUsage.FirstOrDefault(usage => usage.Date.Year == now.Year && usage.Date.Month == now.Month);
                if (durableUsage is not null)
                {
                    turns = Math.Max(turns, durableUsage.Turns);
                    promptTokens = Math.Max(promptTokens, durableUsage.PromptTokens);
                    completionTokens = Math.Max(completionTokens, durableUsage.CompletionTokens);
                    costInMicrodollars = Math.Max(costInMicrodollars, durableUsage.CostInMicrodollars);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to rehydrate durable assistant usage for organization {OrganizationId}", organizationId);
            }
        }

        return new AssistantMonthlyUsage(turns, promptTokens, completionTokens, costInMicrodollars);
    }

    private async Task<AssistantUsageDecision?> TryReserveWindowAsync(
        string organizationId,
        string scope,
        UsageWindow window,
        int limit,
        AssistantUsageLimit usageLimit,
        string message)
    {
        string key = GetTurnKey(organizationId, scope, window.Id);
        long count = await _cache.IncrementAsync(key, 1, window.ResetAtUtc.UtcDateTime);
        if (count <= limit)
            return null;

        if (count == limit + 1)
        {
            logger.LogWarning(
                "Assistant {UsageLimit} limit reached for organization {OrganizationId}",
                usageLimit,
                organizationId);
        }

        return AssistantUsageDecision.Blocked(usageLimit, window.ResetAtUtc, message);
    }

    private static string GetTurnKey(string organizationId, string scope, string windowId)
        => $"organization:{organizationId}:turns:{scope}:{windowId}";

    private static string GetUsageKey(string organizationId, string monthId, string metric)
        => $"organization:{organizationId}:usage:{monthId}:{metric}";

    private static UsageWindow GetMinuteWindow(DateTimeOffset now)
    {
        long id = now.ToUnixTimeSeconds() / 60;
        return new UsageWindow(id.ToString(), DateTimeOffset.FromUnixTimeSeconds((id + 1) * 60));
    }

    private static UsageWindow GetMonthWindow(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return new UsageWindow(start.ToString("yyyyMM"), start.AddMonths(1));
    }

    private static long ToMicrodollars(decimal costUsd)
        => costUsd <= 0 ? 0 : checked((long)Decimal.Ceiling(costUsd * MicrodollarsPerDollar));

    private async Task TryRecordUsageAsync(string? organizationId, AssistantUsageIncrement increment)
    {
        if (String.IsNullOrWhiteSpace(organizationId) || !increment.HasValue)
            return;

        try
        {
            await usageRecorder.RecordAssistantUsageAsync(organizationId, increment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to record durable assistant usage for organization {OrganizationId}", organizationId);
        }
    }

    private sealed record UsageWindow(string Id, DateTimeOffset ResetAtUtc);
}

public enum AssistantUsageLimit
{
    ConcurrentTurns,
    MinuteTurns,
    MonthlyTokens,
    MonthlyCost
}

public sealed record AssistantUsageDecision(
    bool Allowed,
    AssistantUsageLimit? Limit = null,
    DateTimeOffset? ResetAtUtc = null,
    string? Message = null)
{
    public static readonly AssistantUsageDecision AllowedDecision = new(true);

    public static AssistantUsageDecision Blocked(AssistantUsageLimit limit, DateTimeOffset resetAtUtc, string message)
        => new(false, limit, resetAtUtc, message);
}

public sealed record AssistantProviderUsage(long PromptTokens, long CompletionTokens, decimal CostUsd);

public sealed class AssistantTurnReservation : IAsyncDisposable
{
    private readonly ILock? _lease;

    private AssistantTurnReservation(AssistantUsageDecision decision, ILock? lease = null)
    {
        Decision = decision;
        _lease = lease;
    }

    public AssistantUsageDecision Decision { get; }
    public bool Allowed => Decision.Allowed;
    public AssistantUsageLimit? Limit => Decision.Limit;
    public DateTimeOffset? ResetAtUtc => Decision.ResetAtUtc;
    public string? Message => Decision.Message;

    public static AssistantTurnReservation CreateAllowed(ILock? lease = null)
        => new(AssistantUsageDecision.AllowedDecision, lease);

    public static AssistantTurnReservation Blocked(AssistantUsageDecision decision)
        => new(decision);

    public async ValueTask DisposeAsync()
    {
        if (_lease is not null)
            await _lease.DisposeAsync();
    }
}

public sealed record AssistantMonthlyUsage(long Turns, long PromptTokens, long CompletionTokens, long CostInMicrodollars)
{
    public long TotalTokens => PromptTokens + CompletionTokens;
    public decimal CostUsd => CostInMicrodollars / (decimal)MicrodollarsPerDollar;

    private const long MicrodollarsPerDollar = 1_000_000;
}
