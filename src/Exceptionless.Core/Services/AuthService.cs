using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;

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
        string[] userKeys = GetKeys($"user:{emailAddress.Trim().ToLowerInvariant()}", UserFailureLimit, expiresUtc);
        var failures = await _cache.GetAllAsync<string>(userKeys);
        var observedFailures = failures.Where(pair => pair.Value.HasValue && pair.Value.Value.StartsWith("failed:", StringComparison.Ordinal))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Value)).ToArray();
        string reservation = $"pending:{Guid.NewGuid():N}";
        var keys = new List<string>(2);
        try
        {
            string? userKey = await ReserveAsync(userKeys, reservation, expiresUtc);
            if (userKey is null)
                return null;
            keys.Add(userKey);
            if (ipAddress is not null)
            {
                string? ipKey = await ReserveAsync(GetKeys($"ip:{ipAddress}", IpAddressFailureLimit, expiresUtc), reservation, expiresUtc);
                if (ipKey is null)
                {
                    await ReleaseAsync(keys, reservation);
                    return null;
                }
                keys.Add(ipKey);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new LoginAttempt(this, expiresUtc, keys.ToArray(), reservation, observedFailures);
        }
        catch
        {
            await ReleaseAsync(keys, reservation);
            throw;
        }
    }

    public async Task RecordLoginFailureAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        var remaining = attempt.ExpiresUtc - _timeProvider.GetUtcNow().UtcDateTime;
        if (remaining <= TimeSpan.Zero)
            return;
        // A crashed worker stays charged until the boundary, so a slow check cannot
        // outlive its reservation and silently restore admission.
        await Task.WhenAll(attempt.Keys.Select(key => _cache.ReplaceIfEqualAsync(key, $"failed:{attempt.Reservation}", attempt.Reservation, remaining)));
    }

    public async Task RecordLoginSuccessAsync(LoginAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        await ReleaseAsync(attempt.Keys, attempt.Reservation);
        await RemoveFailuresAsync(attempt.ObservedFailures);
    }

    public async Task ClearUserLoginAttemptsAsync(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        var failures = await _cache.GetAllAsync<string>(GetKeys($"user:{emailAddress.Trim().ToLowerInvariant()}", UserFailureLimit, GetWindowExpiration()));
        // Recovery clears completed failures while checks underway retain admission.
        await RemoveFailuresAsync(failures.Where(pair => pair.Value.HasValue && pair.Value.Value.StartsWith("failed:", StringComparison.Ordinal))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Value)));
    }

    private async Task<string?> ReserveAsync(string[] keys, string reservation, DateTime expiresUtc)
    {
        foreach (string key in keys)
            if (await _cache.AddAsync(key, reservation, expiresUtc))
                return key;
        return null;
    }

    private Task ReleaseAsync(IEnumerable<string> keys, string reservation)
        => Task.WhenAll(keys.Select(key => _cache.RemoveIfEqualAsync(key, reservation)));

    private Task RemoveFailuresAsync(IEnumerable<KeyValuePair<string, string>> failures)
        => Task.WhenAll(failures.Select(failure => _cache.RemoveIfEqualAsync(failure.Key, failure.Value)));

    private DateTime GetWindowExpiration() => _timeProvider.GetUtcNow().UtcDateTime.Floor(AttemptWindow).Add(AttemptWindow);

    private static string[] GetKeys(string prefix, int limit, DateTime expiresUtc)
        => Enumerable.Range(0, limit).Select(slot => $"{prefix}:attempts:{expiresUtc.Ticks}:{slot}").ToArray();

    public sealed class LoginAttempt : IAsyncDisposable
    {
        private readonly AuthService _owner;
        internal LoginAttempt(AuthService owner, DateTime expiresUtc, string[] keys, string reservation, KeyValuePair<string, string>[] observedFailures)
        {
            _owner = owner;
            ExpiresUtc = expiresUtc;
            Keys = keys;
            Reservation = reservation;
            ObservedFailures = observedFailures;
        }
        internal DateTime ExpiresUtc { get; }
        internal string[] Keys { get; }
        internal string Reservation { get; }
        internal KeyValuePair<string, string>[] ObservedFailures { get; }
        public ValueTask DisposeAsync() => new(_owner.ReleaseAsync(Keys, Reservation));
    }
}
