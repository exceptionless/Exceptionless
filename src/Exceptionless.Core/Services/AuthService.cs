using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;

namespace Exceptionless.Core.Services;

/// <summary>
/// Tracks temporary password-login failures without changing the user's active state.
/// </summary>
/// <remarks>
/// Five failures per email address or fifteen per IP address block further password logins
/// until the next UTC quarter-hour boundary. Blocked requests do not extend this window.
/// </remarks>
public sealed class AuthService
{
    private const int UserFailureLimit = 5;
    private const int IpAddressFailureLimit = 15;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private readonly ScopedCacheClient _cache;
    private readonly TimeProvider _timeProvider;

    public AuthService(ICacheClient cacheClient, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(cacheClient);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _cache = new ScopedCacheClient(cacheClient, "Auth");
        _timeProvider = timeProvider;
    }

    public async Task<LoginAttempt?> TryBeginLoginAsync(string emailAddress, string? ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        if (ipAddress is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        cancellationToken.ThrowIfCancellationRequested();

        var expiresUtc = GetWindowExpiration();
        string[] userCacheKeys = GetUserCacheKeys(emailAddress, expiresUtc);
        string[] ipAddressCacheKeys = ipAddress is null ? [] : GetIpAddressCacheKeys(ipAddress, expiresUtc);
        var failures = await _cache.GetAllAsync<string>(userCacheKeys.Concat(ipAddressCacheKeys));
        var userFailures = userCacheKeys.Where(key => failures[key].HasValue)
            .Select(key => new KeyValuePair<string, string>(key, failures[key].Value)).ToArray();
        if (userFailures.Length >= UserFailureLimit || ipAddressCacheKeys.Count(key => failures[key].HasValue) >= IpAddressFailureLimit)
            return null;

        return new LoginAttempt(expiresUtc, userCacheKeys, ipAddressCacheKeys, userFailures);
    }

    public async Task RecordLoginFailureAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (_timeProvider.GetUtcNow().UtcDateTime >= attempt.ExpiresUtc)
            return;

        // Atomic additions avoid mutable counter races and cap storage at the failure limits.
        string failureId = Guid.NewGuid().ToString("N");
        await Task.WhenAll(
            RecordFailureAsync(attempt.UserCacheKeys, failureId, attempt.ExpiresUtc),
            RecordFailureAsync(attempt.IpAddressCacheKeys, failureId, attempt.ExpiresUtc));
    }

    public Task RecordLoginSuccessAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        // Only clear failures seen when this attempt began; later failures belong to later attempts.
        return RemoveFailuresAsync(attempt.UserFailures);
    }

    /// <summary>
    /// Clears current user failures after password recovery without clearing IP failures
    /// or changing the account's active state.
    /// </summary>
    public async Task ClearUserLoginAttemptsAsync(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var failures = await _cache.GetAllAsync<string>(GetUserCacheKeys(emailAddress, GetWindowExpiration()));
        await RemoveFailuresAsync(failures.Where(pair => pair.Value.HasValue)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Value)));
    }

    private async Task RecordFailureAsync(string[] cacheKeys, string failureId, DateTime expiresUtc)
    {
        foreach (string cacheKey in cacheKeys)
        {
            if (await _cache.AddAsync(cacheKey, failureId, expiresUtc))
                return;
        }
    }

    private Task RemoveFailuresAsync(IEnumerable<KeyValuePair<string, string>> failures)
        => Task.WhenAll(failures.Select(failure => _cache.RemoveIfEqualAsync(failure.Key, failure.Value)));

    private DateTime GetWindowExpiration() => _timeProvider.GetUtcNow().UtcDateTime.Floor(AttemptWindow).Add(AttemptWindow);

    private static string[] GetUserCacheKeys(string emailAddress, DateTime expiresUtc)
        => Enumerable.Range(0, UserFailureLimit).Select(slot => $"user:{emailAddress.Trim().ToLowerInvariant()}:failures:{expiresUtc.Ticks}:{slot}").ToArray();

    private static string[] GetIpAddressCacheKeys(string ipAddress, DateTime expiresUtc)
        => Enumerable.Range(0, IpAddressFailureLimit).Select(slot => $"ip:{ipAddress}:failures:{expiresUtc.Ticks}:{slot}").ToArray();

    public sealed class LoginAttempt
    {
        internal LoginAttempt(DateTime expiresUtc, string[] userCacheKeys, string[] ipAddressCacheKeys, KeyValuePair<string, string>[] userFailures)
        {
            ExpiresUtc = expiresUtc;
            UserCacheKeys = userCacheKeys;
            IpAddressCacheKeys = ipAddressCacheKeys;
            UserFailures = userFailures;
        }

        internal DateTime ExpiresUtc { get; }
        internal string[] UserCacheKeys { get; }
        internal string[] IpAddressCacheKeys { get; }
        internal KeyValuePair<string, string>[] UserFailures { get; }
    }
}
