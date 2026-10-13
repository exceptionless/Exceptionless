using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

/// <summary>
/// Coordinates password checks and temporary failures across authentication callers.
/// </summary>
public sealed class AuthService
{
    private const int UserFailureLimit = 5;
    private const int IpAddressFailureLimit = 15;
    private const int MaximumAdmissionWaiters = 100;
    private const int MaximumUpdateAttempts = 8;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AdmissionWaitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AdmissionRetryDelay = TimeSpan.FromMilliseconds(50);
    private readonly ICacheClient _cache;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private int _admissionWaiters;

    public AuthService(ICacheClient cacheClient, TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(cacheClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _cache = new ScopedCacheClient(cacheClient, "Auth");
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<AuthService>();
    }

    public async Task<LoginAttempt?> TryBeginLoginAsync(string emailAddress, string? ipAddress, CancellationToken cancellationToken = default)
        => (await TryBeginLoginCoreAsync(emailAddress, ipAddress, cancellationToken)).Attempt;

    /// <summary>
    /// Lets Basic-password callers retry pending checks for up to two seconds, with at most
    /// 100 waiters per service instance. Completed failures deny admission immediately.
    /// In-flight cache operations remain subject to the provider's own timeout.
    /// </summary>
    public async Task<LoginAttempt?> WaitForLoginAsync(string emailAddress, string? ipAddress, CancellationToken cancellationToken = default)
    {
        long started = _timeProvider.GetTimestamp();
        long lastAttempt = started;
        var result = await TryBeginLoginCoreAsync(emailAddress, ipAddress, cancellationToken);
        if (result.Attempt is not null || result.BlockedBudget is null)
            return result.Attempt;

        int waiters = Interlocked.Increment(ref _admissionWaiters);
        try
        {
            if (waiters > MaximumAdmissionWaiters)
                return null;

            while (result.Attempt is null && result.BlockedBudget is not null)
            {
                if (!await WaitForCapacityAsync(result.BlockedBudget.Value, started, lastAttempt, cancellationToken))
                    return null;

                lastAttempt = _timeProvider.GetTimestamp();
                result = await TryBeginLoginCoreAsync(emailAddress, ipAddress, cancellationToken);
                if (_timeProvider.GetElapsedTime(started) >= AdmissionWaitTimeout)
                {
                    if (result.Attempt is not null)
                        await result.Attempt.DisposeAsync();

                    return null;
                }
            }

            return result.Attempt;
        }
        finally
        {
            Interlocked.Decrement(ref _admissionWaiters);
        }
    }

    private async Task<bool> WaitForCapacityAsync((string Key, int Limit) budget, long started, long lastAttempt, CancellationToken cancellationToken)
    {
        while (_timeProvider.GetElapsedTime(started) < AdmissionWaitTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = await _cache.GetAsync<LoginState>(budget.Key);
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = AdmissionWaitTimeout - _timeProvider.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
                return false;

            if (!entry.HasValue || entry.Value.Pending + entry.Value.TotalFailures - entry.Value.ClearedFailures < budget.Limit)
            {
                // A conflicting write can leave capacity available. Pace admission retries too.
                var delay = AdmissionRetryDelay - _timeProvider.GetElapsedTime(lastAttempt);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(remaining < delay ? remaining : delay, _timeProvider, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                return _timeProvider.GetElapsedTime(started) < AdmissionWaitTimeout;
            }

            if (entry.Value.Pending == 0)
                return false;

            await Task.Delay(remaining < AdmissionRetryDelay ? remaining : AdmissionRetryDelay, _timeProvider, cancellationToken);
        }

        return false;
    }

    private async Task<(LoginAttempt? Attempt, (string Key, int Limit)? BlockedBudget)> TryBeginLoginCoreAsync(string emailAddress, string? ipAddress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        if (ipAddress is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        cancellationToken.ThrowIfCancellationRequested();
        var expiresUtc = GetWindowExpiration();
        var budgets = new List<(string Key, int Limit)>(2) { (GetUserCacheKey(emailAddress, expiresUtc), UserFailureLimit) };
        if (ipAddress is not null)
            budgets.Add(($"ip:{ipAddress}:attempts:{expiresUtc.Ticks}", IpAddressFailureLimit));

        var reservations = new List<Reservation>(2);
        try
        {
            foreach (var budget in budgets)
            {
                var state = await TryUpdateAttemptsAsync(budget.Key, expiresUtc, current =>
                    current.Pending + current.TotalFailures - current.ClearedFailures < budget.Limit
                        ? current with { Pending = current.Pending + 1 }
                        : null, cancellationToken);
                if (state is null)
                {
                    await ReleaseAsync(reservations, expiresUtc);
                    return (null, budget);
                }

                // Only account successes clear failures, and only those observed at admission.
                reservations.Add(new Reservation(budget.Key, state.Epoch, reservations.Count == 0 ? state.TotalFailures : null));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return (new LoginAttempt(this, expiresUtc, reservations.ToArray()), null);
        }
        catch
        {
            await ReleaseAsync(reservations, expiresUtc);
            throw;
        }
    }

    public async Task RecordLoginFailureAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        // Claim the outcome before I/O: a failed or ambiguous write must remain charged.
        if (!attempt.TryComplete())
            return;

        await Task.WhenAll(attempt.Reservations.Select(reservation =>
            TryUpdateAttemptsAsync(reservation.Key, attempt.ExpiresUtc, current =>
                current.Epoch == reservation.Epoch && current.Pending > 0
                    ? current with { Pending = current.Pending - 1, TotalFailures = current.TotalFailures + 1 }
                    : null)));
    }

    public async Task RecordLoginSuccessAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (attempt.TryComplete())
            await ReleaseAsync(attempt.Reservations, attempt.ExpiresUtc, clearFailures: true);
    }

    public Task ClearUserLoginAttemptsAsync(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var expiresUtc = GetWindowExpiration();
        string key = GetUserCacheKey(emailAddress, expiresUtc);
        LoginState? observed = null;
        return TryUpdateAttemptsAsync(key, expiresUtc, current =>
        {
            // Fix the first snapshot so retries cannot erase newer failures.
            observed ??= current;
            return current.Epoch == observed.Epoch && current.ClearedFailures < observed.TotalFailures
                ? current with { ClearedFailures = observed.TotalFailures }
                : null;
        });
    }

    private async Task<LoginState?> TryUpdateAttemptsAsync(string key, DateTime expiresUtc, Func<LoginState, LoginState?> update, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < MaximumUpdateAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = await _cache.GetAsync<LoginState>(key);
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = expiresUtc - _timeProvider.GetUtcNow().UtcDateTime;
            if (remaining <= CacheClientExtensions.MinimumExpiration)
                return null;

            var current = entry.HasValue ? entry.Value : new LoginState(Guid.NewGuid(), 0, 0, 0);
            var updated = update(current);
            if (updated is null)
                return null;

            bool committed;
            if (!entry.HasValue)
                committed = await _cache.AddAsync(key, updated, remaining);
            else if (updated.Pending == 0 && updated.TotalFailures == updated.ClearedFailures)
                committed = await _cache.RemoveIfEqualAsync(key, current);
            else
                committed = await _cache.ReplaceIfEqualAsync(key, updated, current, remaining);
            if (committed)
                return updated;
        }

        // Exhausted contention fails closed. Exceptions are never retried: they may have committed.
        return null;
    }

    private async Task ReleaseAsync(IEnumerable<Reservation> reservations, DateTime expiresUtc, bool clearFailures = false)
    {
        foreach (var reservation in reservations)
        {
            try
            {
                await TryUpdateAttemptsAsync(reservation.Key, expiresUtc, current =>
                    current.Epoch == reservation.Epoch && current.Pending > 0
                        ? current with
                        {
                            Pending = current.Pending - 1,
                            ClearedFailures = clearFailures && reservation.ObservedFailures.HasValue
                                ? Math.Max(current.ClearedFailures, reservation.ObservedFailures.Value)
                                : current.ClearedFailures
                        }
                        : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing login admission reservation: {Message}", ex.Message);
            }
        }
    }

    private DateTime GetWindowExpiration() => _timeProvider.GetUtcNow().UtcDateTime.Floor(AttemptWindow).Add(AttemptWindow);

    private static string GetUserCacheKey(string emailAddress, DateTime expiresUtc)
        => $"user:{emailAddress.Trim().ToLowerInvariant()}:attempts:{expiresUtc.Ticks}";

    // Epochs protect recreated entries; failure watermarks preserve failures newer than admission.
    internal sealed record LoginState(Guid Epoch, int Pending, long TotalFailures, long ClearedFailures);
    internal sealed record Reservation(string Key, Guid Epoch, long? ObservedFailures);

    /// <summary>
    /// Owns one admitted password check. Disposal releases only an unfinished check.
    /// </summary>
    public sealed class LoginAttempt : IAsyncDisposable
    {
        private readonly AuthService _owner;
        private int _completed;

        internal LoginAttempt(AuthService owner, DateTime expiresUtc, Reservation[] reservations)
        {
            _owner = owner;
            ExpiresUtc = expiresUtc;
            Reservations = reservations;
        }

        internal DateTime ExpiresUtc { get; }
        internal Reservation[] Reservations { get; }

        internal bool TryComplete() => Interlocked.CompareExchange(ref _completed, 1, 0) == 0;

        public ValueTask DisposeAsync() => TryComplete() ? new(_owner.ReleaseAsync(Reservations, ExpiresUtc)) : ValueTask.CompletedTask;
    }
}
