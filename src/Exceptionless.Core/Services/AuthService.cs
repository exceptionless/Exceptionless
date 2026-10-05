using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services;

/// <summary>
/// Coordinates password checks and temporary failures across authentication callers.
/// </summary>
public sealed class AuthService
{
    private const int UserFailureLimit = 5;
    private const int IpAddressFailureLimit = 15;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private readonly ScopedCacheClient _cache;
    private readonly ILogger<AuthService> _logger;
    private readonly TimeProvider _timeProvider;

    public AuthService(ICacheClient cacheClient, TimeProvider timeProvider)
        : this(cacheClient, timeProvider, NullLogger<AuthService>.Instance)
    {
    }

    public AuthService(ICacheClient cacheClient, TimeProvider timeProvider, ILogger<AuthService> logger)
    {
        ArgumentNullException.ThrowIfNull(cacheClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = new ScopedCacheClient(cacheClient, "Auth");
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<LoginAttempt?> TryBeginLoginAsync(string emailAddress, string? ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        if (ipAddress is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        cancellationToken.ThrowIfCancellationRequested();

        var expiresUtc = GetWindowExpiration();
        string[] userCacheKeys = GetCacheKeys($"user:{emailAddress.Trim().ToLowerInvariant()}", UserFailureLimit, expiresUtc);
        var failures = await _cache.GetAllAsync<string>(userCacheKeys);
        var observedFailures = failures.Where(pair => pair.Value.HasValue && pair.Value.Value.StartsWith("failed:", StringComparison.Ordinal))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Value)).ToArray();
        string reservation = $"pending:{Guid.NewGuid():N}";
        var reservedCacheKeys = new List<string>(2);
        try
        {
            string? userCacheKey = await ReserveCacheKeyAsync(userCacheKeys, reservation, expiresUtc);
            if (userCacheKey is null)
                return null;

            reservedCacheKeys.Add(userCacheKey);

            if (ipAddress is not null)
            {
                string? ipAddressCacheKey = await ReserveCacheKeyAsync(GetCacheKeys($"ip:{ipAddress}", IpAddressFailureLimit, expiresUtc), reservation, expiresUtc);
                if (ipAddressCacheKey is null)
                {
                    await ReleaseCacheKeysAsync(reservedCacheKeys, reservation);
                    return null;
                }

                reservedCacheKeys.Add(ipAddressCacheKey);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new LoginAttempt(this, expiresUtc, reservedCacheKeys.ToArray(), reservation, observedFailures);
        }
        catch (Exception exception)
        {
            try
            {
                await ReleaseCacheKeysAsync(reservedCacheKeys, reservation);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError("Failed to release login admission reservations after {FailureType}: {CleanupFailureType}",
                    exception.GetType().Name, cleanupException.GetType().Name);
            }

            throw;
        }
    }

    public async Task RecordLoginFailureAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var remaining = attempt.ExpiresUtc - _timeProvider.GetUtcNow().UtcDateTime;
        if (remaining <= TimeSpan.Zero)
            return;

        // Pending checks and completed failures share the fixed-window admission budget.
        // Reservations expire at the boundary even if a check is still running.
        await Task.WhenAll(attempt.CacheKeys.Select(cacheKey => _cache.ReplaceIfEqualAsync(cacheKey, $"failed:{attempt.Reservation}", attempt.Reservation, remaining)));
    }

    public async Task RecordLoginSuccessAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        await ReleaseCacheKeysAsync(attempt.CacheKeys, attempt.Reservation);
        await RemoveFailuresAsync(attempt.ObservedFailures);
    }

    public async Task ClearUserLoginAttemptsAsync(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var failures = await _cache.GetAllAsync<string>(GetCacheKeys($"user:{emailAddress.Trim().ToLowerInvariant()}", UserFailureLimit, GetWindowExpiration()));
        // Recovery clears completed failures while checks underway retain admission.
        await RemoveFailuresAsync(failures.Where(pair => pair.Value.HasValue && pair.Value.Value.StartsWith("failed:", StringComparison.Ordinal))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Value)));
    }

    private async Task<string?> ReserveCacheKeyAsync(string[] cacheKeys, string reservation, DateTime expiresUtc)
    {
        foreach (string cacheKey in cacheKeys)
            if (await _cache.AddAsync(cacheKey, reservation, expiresUtc))
                return cacheKey;

        return null;
    }

    private Task ReleaseCacheKeysAsync(IEnumerable<string> cacheKeys, string reservation)
        => Task.WhenAll(cacheKeys.Select(cacheKey => _cache.RemoveIfEqualAsync(cacheKey, reservation)));

    private Task RemoveFailuresAsync(IEnumerable<KeyValuePair<string, string>> failures)
        => Task.WhenAll(failures.Select(failure => _cache.RemoveIfEqualAsync(failure.Key, failure.Value)));

    private DateTime GetWindowExpiration() => _timeProvider.GetUtcNow().UtcDateTime.Floor(AttemptWindow).Add(AttemptWindow);

    // The window selects expiration; separate cache entries atomically reserve admission.
    private static string[] GetCacheKeys(string cacheKeyPrefix, int limit, DateTime expiresUtc)
        => Enumerable.Range(0, limit).Select(index => $"{cacheKeyPrefix}:attempts:{expiresUtc.Ticks}:{index}").ToArray();

    public sealed class LoginAttempt : IAsyncDisposable
    {
        private readonly AuthService _owner;

        internal LoginAttempt(AuthService owner, DateTime expiresUtc, string[] cacheKeys, string reservation, KeyValuePair<string, string>[] observedFailures)
        {
            _owner = owner;
            ExpiresUtc = expiresUtc;
            CacheKeys = cacheKeys;
            Reservation = reservation;
            ObservedFailures = observedFailures;
        }

        internal DateTime ExpiresUtc { get; }
        internal string[] CacheKeys { get; }
        internal string Reservation { get; }
        internal KeyValuePair<string, string>[] ObservedFailures { get; }

        public ValueTask DisposeAsync() => new(_owner.ReleaseCacheKeysAsync(CacheKeys, Reservation));
    }
}
