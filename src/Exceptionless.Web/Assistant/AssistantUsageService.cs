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
    private readonly Func<string, Task<Organization?>>? _loadOrganizationAsync = organizationRepository is null
        ? null
        : organizationId => organizationRepository.GetByIdAsync(organizationId, options => options.Cache());

    internal AssistantUsageService(
        ICacheClient cacheClient,
        ILockProvider lockProvider,
        IAssistantUsageRecorder usageRecorder,
        AppOptions appOptions,
        TimeProvider timeProvider,
        ILogger<AssistantUsageService> logger,
        Func<string, Task<Organization?>> loadOrganizationAsync)
        : this(cacheClient, lockProvider, usageRecorder, appOptions, timeProvider, logger)
    {
        _loadOrganizationAsync = loadOrganizationAsync;
    }

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

    public async Task<AssistantProviderReservation> RecordProviderRequestStartedAsync(string? organizationId, int providerInputCharacters = 0)
    {
        await TryRecordUsageAsync(organizationId, new AssistantUsageIncrement { ProviderRequests = 1 });
        if (String.IsNullOrWhiteSpace(organizationId) || providerInputCharacters <= 0)
            return AssistantProviderReservation.Empty;

        var now = timeProvider.GetUtcNow();
        await EnsureMonthlyUsageCacheAsync(organizationId, now);
        var month = GetMonthWindow(now);
        long promptTokens = providerInputCharacters;
        long completionTokens = AssistantLimits.MaximumOutputTokens;
        long costInMicrodollars = ToMicrodollars(
            promptTokens * AssistantLimits.MaximumProviderPromptPricePerMillionTokens / 1_000_000m
            + completionTokens * AssistantLimits.MaximumProviderCompletionPricePerMillionTokens / 1_000_000m);
        var reservationExpiresAtUtc = month.ResetAtUtc.AddSeconds(AssistantLimits.MaximumTurnDurationSeconds + 30);
        await Task.WhenAll(
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "reserved-prompt-tokens"), promptTokens, reservationExpiresAtUtc.UtcDateTime),
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "reserved-completion-tokens"), completionTokens, reservationExpiresAtUtc.UtcDateTime),
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "reserved-cost-microdollars"), costInMicrodollars, reservationExpiresAtUtc.UtcDateTime));

        return new AssistantProviderReservation(promptTokens, completionTokens, costInMicrodollars)
        {
            MonthId = month.Id,
            ExpiresAtUtc = reservationExpiresAtUtc
        };
    }

    public async Task<AssistantProviderUsageLifecycle> StartProviderRequestAsync(string? organizationId, int providerInputCharacters = 0)
    {
        var reservation = await RecordProviderRequestStartedAsync(organizationId, providerInputCharacters);
        return new AssistantProviderUsageLifecycle(this, organizationId, reservation);
    }

    public async Task RecordProviderUsageAsync(
        string? organizationId,
        AssistantProviderUsage usage,
        bool providerRequestAlreadyRecorded = false,
        AssistantProviderReservation? reservation = null)
    {
        if (String.IsNullOrWhiteSpace(organizationId))
            return;

        await RecordProviderUsageAsync(
            organizationId,
            Math.Max(0, usage.PromptTokens),
            Math.Max(0, usage.CompletionTokens),
            ToMicrodollars(usage.CostUsd),
            providerRequestAlreadyRecorded,
            reservation);
    }

    internal async Task RecordUnreconciledProviderReservationAsync(
        string? organizationId,
        AssistantProviderReservation reservation)
    {
        if (String.IsNullOrWhiteSpace(organizationId) || !reservation.HasValue)
            return;

        logger.LogWarning(
            "Recording conservative assistant provider usage for organization {OrganizationId} because actual usage was not received",
            organizationId);

        await RecordProviderUsageAsync(
            organizationId,
            reservation.PromptTokens,
            reservation.CompletionTokens,
            reservation.CostInMicrodollars,
            providerRequestAlreadyRecorded: true,
            reservation);
    }

    private async Task RecordProviderUsageAsync(
        string organizationId,
        long promptTokens,
        long completionTokens,
        long costInMicrodollars,
        bool providerRequestAlreadyRecorded,
        AssistantProviderReservation? reservation)
    {
        UsageWindow month;
        if (reservation is { MonthId: not null, ExpiresAtUtc: not null })
        {
            month = new UsageWindow(reservation.MonthId, reservation.ExpiresAtUtc.Value);
        }
        else
        {
            var now = timeProvider.GetUtcNow();
            await EnsureMonthlyUsageCacheAsync(organizationId, now);
            month = GetMonthWindow(now);
        }
        await UpdateProviderUsageCacheAsync(
            organizationId,
            month,
            promptTokens,
            completionTokens,
            costInMicrodollars,
            reservation,
            reverse: false);

        try
        {
            await RecordUsageAsync(organizationId, new AssistantUsageIncrement
            {
                ProviderRequests = providerRequestAlreadyRecorded ? 0 : 1,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                CostInMicrodollars = costInMicrodollars
            });
        }
        catch
        {
            await UpdateProviderUsageCacheAsync(
                organizationId,
                month,
                promptTokens,
                completionTokens,
                costInMicrodollars,
                reservation,
                reverse: true);
            throw;
        }
    }

    private Task UpdateProviderUsageCacheAsync(
        string organizationId,
        UsageWindow month,
        long promptTokens,
        long completionTokens,
        long costInMicrodollars,
        AssistantProviderReservation? reservation,
        bool reverse)
    {
        int direction = reverse ? -1 : 1;
        var tasks = new List<Task>
        {
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "prompt-tokens"), direction * promptTokens, month.ResetAtUtc.UtcDateTime),
            _cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "completion-tokens"), direction * completionTokens, month.ResetAtUtc.UtcDateTime)
        };

        if (costInMicrodollars > 0)
            tasks.Add(_cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "cost-microdollars"), direction * costInMicrodollars, month.ResetAtUtc.UtcDateTime));
        if (reservation is { HasValue: true })
        {
            tasks.Add(_cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "reserved-prompt-tokens"), -direction * reservation.PromptTokens, month.ResetAtUtc.UtcDateTime));
            tasks.Add(_cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "reserved-completion-tokens"), -direction * reservation.CompletionTokens, month.ResetAtUtc.UtcDateTime));
            tasks.Add(_cache.IncrementAsync(GetUsageKey(organizationId, month.Id, "reserved-cost-microdollars"), -direction * reservation.CostInMicrodollars, month.ResetAtUtc.UtcDateTime));
        }

        return Task.WhenAll(tasks);
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
        await EnsureMonthlyUsageCacheAsync(organizationId, now);
        long turns = await _cache.GetAsync<long>(GetTurnKey(organizationId, "month", month.Id), 0);
        long promptTokens = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "prompt-tokens"), 0);
        long completionTokens = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "completion-tokens"), 0);
        long costInMicrodollars = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "cost-microdollars"), 0);
        long reservedPromptTokens = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "reserved-prompt-tokens"), 0);
        long reservedCompletionTokens = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "reserved-completion-tokens"), 0);
        long reservedCostInMicrodollars = await _cache.GetAsync<long>(GetUsageKey(organizationId, month.Id, "reserved-cost-microdollars"), 0);

        return new AssistantMonthlyUsage(
            turns,
            promptTokens + Math.Max(0, reservedPromptTokens),
            completionTokens + Math.Max(0, reservedCompletionTokens),
            costInMicrodollars + Math.Max(0, reservedCostInMicrodollars));
    }

    private async Task EnsureMonthlyUsageCacheAsync(string organizationId, DateTimeOffset now)
    {
        var month = GetMonthWindow(now);
        var cacheValues = await GetMonthlyUsageCacheValuesAsync(organizationId, month.Id);
        if (cacheValues.Values.All(value => value.HasValue))
            return;

        await using var initializationLock = await lockProvider.AcquireAsync(
            $"assistant-usage-initialize:{organizationId}:{month.Id}",
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15));
        cacheValues = await GetMonthlyUsageCacheValuesAsync(organizationId, month.Id);
        if (cacheValues.Values.All(value => value.HasValue))
            return;

        var durableUsage = _loadOrganizationAsync is null
            ? null
            : (await _loadOrganizationAsync(organizationId))?.AssistantUsage.FirstOrDefault(usage => usage.Date.Year == now.Year && usage.Date.Month == now.Month);

        TimeSpan expiresIn = month.ResetAtUtc - now;
        var seedValues = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [GetTurnKey(organizationId, "month", month.Id)] = durableUsage?.Turns ?? 0,
            [GetUsageKey(organizationId, month.Id, "prompt-tokens")] = durableUsage?.PromptTokens ?? 0,
            [GetUsageKey(organizationId, month.Id, "completion-tokens")] = durableUsage?.CompletionTokens ?? 0,
            [GetUsageKey(organizationId, month.Id, "cost-microdollars")] = durableUsage?.CostInMicrodollars ?? 0,
            [GetUsageKey(organizationId, month.Id, "reserved-prompt-tokens")] = 0,
            [GetUsageKey(organizationId, month.Id, "reserved-completion-tokens")] = 0,
            [GetUsageKey(organizationId, month.Id, "reserved-cost-microdollars")] = 0
        };
        await Task.WhenAll(seedValues
            .Where(pair => !cacheValues[pair.Key].HasValue)
            .Select(pair => _cache.SetAsync(pair.Key, pair.Value, expiresIn)));
    }

    private Task<IDictionary<string, CacheValue<long>>> GetMonthlyUsageCacheValuesAsync(string organizationId, string monthId)
        => _cache.GetAllAsync<long>(
        [
            GetTurnKey(organizationId, "month", monthId),
            GetUsageKey(organizationId, monthId, "prompt-tokens"),
            GetUsageKey(organizationId, monthId, "completion-tokens"),
            GetUsageKey(organizationId, monthId, "cost-microdollars"),
            GetUsageKey(organizationId, monthId, "reserved-prompt-tokens"),
            GetUsageKey(organizationId, monthId, "reserved-completion-tokens"),
            GetUsageKey(organizationId, monthId, "reserved-cost-microdollars")
        ]);

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
        try
        {
            await RecordUsageAsync(organizationId, increment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to record durable assistant usage for organization {OrganizationId}", organizationId);
        }
    }

    private Task RecordUsageAsync(string? organizationId, AssistantUsageIncrement increment)
        => String.IsNullOrWhiteSpace(organizationId) || !increment.HasValue
            ? Task.CompletedTask
            : usageRecorder.RecordAssistantUsageAsync(organizationId, increment);

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

public sealed record AssistantProviderReservation(long PromptTokens, long CompletionTokens, long CostInMicrodollars)
{
    public static AssistantProviderReservation Empty { get; } = new(0, 0, 0);
    public bool HasValue => PromptTokens > 0 || CompletionTokens > 0 || CostInMicrodollars > 0;
    internal string? MonthId { get; init; }
    internal DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class AssistantProviderUsageLifecycle : IAsyncDisposable
{
    private readonly AssistantUsageService _usageService;
    private readonly string? _organizationId;
    private readonly AssistantProviderReservation _reservation;
    private int _completionState;

    internal AssistantProviderUsageLifecycle(
        AssistantUsageService usageService,
        string? organizationId,
        AssistantProviderReservation reservation)
    {
        _usageService = usageService;
        _organizationId = organizationId;
        _reservation = reservation;
    }

    public async Task ReconcileAsync(AssistantProviderUsage usage)
    {
        if (Interlocked.CompareExchange(ref _completionState, 1, 0) != 0)
            throw new InvalidOperationException("Provider usage has already been completed.");

        try
        {
            await _usageService.RecordProviderUsageAsync(
                _organizationId,
                usage,
                providerRequestAlreadyRecorded: true,
                _reservation);
            Volatile.Write(ref _completionState, 2);
        }
        catch
        {
            Volatile.Write(ref _completionState, 0);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _completionState, 1, 0) != 0)
            return;

        try
        {
            await _usageService.RecordUnreconciledProviderReservationAsync(_organizationId, _reservation);
            Volatile.Write(ref _completionState, 2);
        }
        catch
        {
            Volatile.Write(ref _completionState, 0);
            throw;
        }
    }
}

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
