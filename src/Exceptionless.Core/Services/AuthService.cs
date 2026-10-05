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
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private readonly ScopedCacheClient _cache;
    private readonly ILogger<AuthService> _logger;
    private readonly TimeProvider _timeProvider;

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
        string[] userCacheKeys = GetUserCacheKeys(emailAddress, expiresUtc);
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
                string? ipAddressCacheKey = await ReserveCacheKeyAsync(GetIpAddressCacheKeys(ipAddress, expiresUtc), reservation, expiresUtc);
                if (ipAddressCacheKey is null)
                {
                    await ReleaseCacheKeysAsync(reservedCacheKeys, reservation);
                    return null;
                }

                reservedCacheKeys.Add(ipAddressCacheKey);
            }

            cancellationToken.ThrowIfCancellationRequested();

            string[] cacheKeys = reservedCacheKeys.ToArray();

            return new LoginAttempt(expiresUtc, cacheKeys, reservation, observedFailures, () => ReleaseCacheKeysAsync(cacheKeys, reservation));
        }
        catch
        {
            await ReleaseCacheKeysAsync(reservedCacheKeys, reservation);

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

        var failures = await _cache.GetAllAsync<string>(GetUserCacheKeys(emailAddress, GetWindowExpiration()));
        // Recovery clears completed failures while checks underway retain admission.
        await RemoveFailuresAsync(failures.Where(pair => pair.Value.HasValue && pair.Value.Value.StartsWith("failed:", StringComparison.Ordinal))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Value)));
    }

    /// <summary>
    /// Atomically reserves the first available cache entry until the captured window expires.
    /// </summary>
    /// <param name="cacheKeys">The entries belonging to one user's or IP address's admission budget.</param>
    /// <param name="reservation">The unique value used to conditionally release or charge the entry.</param>
    /// <param name="expiresUtc">The expiration captured before reserving either admission budget.</param>
    /// <returns>The reserved cache key, or <see langword="null"/> when the admission budget is exhausted.</returns>
    private async Task<string?> ReserveCacheKeyAsync(string[] cacheKeys, string reservation, DateTime expiresUtc)
    {
        foreach (string cacheKey in cacheKeys)
            if (await _cache.AddAsync(cacheKey, reservation, expiresUtc))
                return cacheKey;

        return null;
    }

    /// <summary>
    /// Releases every entry still owned by the reservation. Cleanup failures are logged and remain
    /// charged until expiration, so disposal cannot replace an in-flight error or cancellation.
    /// </summary>
    private async Task ReleaseCacheKeysAsync(IEnumerable<string> cacheKeys, string reservation)
    {
        foreach (string cacheKey in cacheKeys)
        {
            try
            {
                await _cache.RemoveIfEqualAsync(cacheKey, reservation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing login admission reservation: {Message}", ex.Message);
            }
        }
    }

    private Task RemoveFailuresAsync(IEnumerable<KeyValuePair<string, string>> failures)
        => Task.WhenAll(failures.Select(failure => _cache.RemoveIfEqualAsync(failure.Key, failure.Value)));

    private DateTime GetWindowExpiration() => _timeProvider.GetUtcNow().UtcDateTime.Floor(AttemptWindow).Add(AttemptWindow);

    /// <summary>
    /// Gets the user admission cache keys for the captured window, normalizing email casing and whitespace.
    /// </summary>
    /// <param name="emailAddress">The email identity shared by interactive and Basic password authentication.</param>
    /// <param name="expiresUtc">The captured window expiration, also used by the IP admission budget.</param>
    /// <returns>The five cache entries sharing the user's fixed-window admission budget.</returns>
    private static string[] GetUserCacheKeys(string emailAddress, DateTime expiresUtc)
    {
        string normalizedEmailAddress = emailAddress.Trim().ToLowerInvariant();

        return Enumerable.Range(0, UserFailureLimit).Select(index => $"user:{normalizedEmailAddress}:attempts:{expiresUtc.Ticks}:{index}").ToArray();
    }

    /// <summary>
    /// Gets the IP admission cache keys for the same captured window as the user reservation.
    /// </summary>
    /// <param name="ipAddress">The client IP address supplied by the authentication caller.</param>
    /// <param name="expiresUtc">The same captured expiration used by the user admission budget.</param>
    /// <returns>The fifteen cache entries sharing the IP address's fixed-window admission budget.</returns>
    private static string[] GetIpAddressCacheKeys(string ipAddress, DateTime expiresUtc)
        => Enumerable.Range(0, IpAddressFailureLimit).Select(index => $"ip:{ipAddress}:attempts:{expiresUtc.Ticks}:{index}").ToArray();

    /// <summary>
    /// Owns one login admission reservation and releases unfinished entries when disposed.
    /// </summary>
    public sealed class LoginAttempt : IAsyncDisposable
    {
        private readonly Func<Task> _releaseAsync;

        internal LoginAttempt(DateTime expiresUtc, string[] cacheKeys, string reservation, KeyValuePair<string, string>[] observedFailures, Func<Task> releaseAsync)
        {
            _releaseAsync = releaseAsync;
            ExpiresUtc = expiresUtc;
            CacheKeys = cacheKeys;
            Reservation = reservation;
            ObservedFailures = observedFailures;
        }

        internal DateTime ExpiresUtc { get; }
        internal string[] CacheKeys { get; }
        internal string Reservation { get; }
        internal KeyValuePair<string, string>[] ObservedFailures { get; }

        public ValueTask DisposeAsync() => new(_releaseAsync());
    }
}
