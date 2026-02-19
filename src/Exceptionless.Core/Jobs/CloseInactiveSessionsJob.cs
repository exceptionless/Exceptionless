using System.Diagnostics;
using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Closes inactive user sessions.", InitialDelay = "30s", Interval = "30s")]
public class CloseInactiveSessionsJob : JobWithLockBase, IHealthCheck
{
    private readonly IEventRepository _eventRepository;
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;
    private readonly JsonSerializerOptions _jsonOptions;
    private DateTime? _lastActivity;

    public CloseInactiveSessionsJob(IEventRepository eventRepository, ICacheClient cacheClient,
        JsonSerializerOptions jsonOptions,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _eventRepository = eventRepository;
        _cache = cacheClient;
        _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(1), timeProvider, resiliencePolicyProvider, loggerFactory);
        _jsonOptions = jsonOptions;
    }

    protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(CloseInactiveSessionsJob), TimeSpan.FromMinutes(15), new CancellationToken(true));
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastActivity = _timeProvider.GetUtcNow().UtcDateTime;
        var results = await _eventRepository.GetOpenSessionsAsync(_timeProvider.GetUtcNow().UtcDateTime.SubtractMinutes(1), o => o.SearchAfterPaging().PageLimit(100));
        int sessionsClosed = 0;
        int totalSessions = 0;
        if (results.Documents.Count == 0)
            return JobResult.Success;

        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            var inactivePeriodUtc = _timeProvider.GetUtcNow().UtcDateTime.Subtract(DefaultInactivePeriod);
            var sessionsToUpdate = new List<PersistentEvent>(results.Documents.Count);
            var cacheKeysToRemove = new List<string>(results.Documents.Count * 2);
            var existingSessionHeartbeatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var documents = results.Documents as IReadOnlyList<PersistentEvent> ?? [.. results.Documents];
            var heartbeats = await GetHeartbeatsBatchAsync(documents);

            for (int i = 0; i < documents.Count; i++)
            {
                var sessionStart = documents[i];
                var lastActivityUtc = sessionStart.Date.UtcDateTime.AddSeconds((double)sessionStart.Value.GetValueOrDefault());
                var heartbeatResult = heartbeats[i];

                bool closeDuplicate = heartbeatResult?.CacheKey is not null && existingSessionHeartbeatIds.Contains(heartbeatResult.CacheKey);
                if (heartbeatResult?.CacheKey is not null && !closeDuplicate)
                    existingSessionHeartbeatIds.Add(heartbeatResult.CacheKey);

                if (heartbeatResult is not null && (closeDuplicate || heartbeatResult.Close || heartbeatResult.ActivityUtc > lastActivityUtc))
                    sessionStart.UpdateSessionStart(heartbeatResult.ActivityUtc, isSessionEnd: closeDuplicate || heartbeatResult.Close || heartbeatResult.ActivityUtc <= inactivePeriodUtc);
                else if (lastActivityUtc <= inactivePeriodUtc)
                    sessionStart.UpdateSessionStart(lastActivityUtc, isSessionEnd: true);
                else
                    continue;

                sessionsToUpdate.Add(sessionStart);
                if (heartbeatResult?.CacheKey is not null)
                {
                    cacheKeysToRemove.Add(heartbeatResult.CacheKey);
                    if (heartbeatResult.Close)
                        cacheKeysToRemove.Add($"{heartbeatResult.CacheKey}-close");
                }

                Debug.Assert(sessionStart.Value is not null && sessionStart.Value >= 0, "Session start value cannot be a negative number.");
            }

            totalSessions += results.Documents.Count;
            sessionsClosed += sessionsToUpdate.Count;

            if (sessionsToUpdate.Count > 0)
                await _eventRepository.SaveAsync(sessionsToUpdate);

            if (cacheKeysToRemove.Count > 0)
                await _cache.RemoveAllAsync(cacheKeysToRemove);

            _logger.LogInformation("Closing {SessionClosedCount} of {SessionCount} sessions", sessionsToUpdate.Count, results.Documents.Count);

            // Sleep so we are not hammering the backend.
            await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;

            if (results.Documents.Count > 0)
            {
                await context.RenewLockAsync();
                _lastActivity = _timeProvider.GetUtcNow().UtcDateTime;
            }
        }
        _logger.LogInformation("Done checking active sessions. Closed {SessionClosedCount} of {SessionCount} sessions", sessionsClosed, totalSessions);

        return JobResult.Success;
    }

    private async Task<HeartbeatResult?[]> GetHeartbeatsBatchAsync(IReadOnlyList<PersistentEvent> sessions)
    {
        var allHeartbeatKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sessionKeyMap = new (string? SessionIdKey, string? UserIdentityKey)[sessions.Count];

        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            string? sessionIdKey = null;
            string? userIdentityKey = null;

            string? sessionId = session.GetSessionId();
            if (!String.IsNullOrWhiteSpace(sessionId))
            {
                sessionIdKey = $"Project:{session.ProjectId}:heartbeat:{sessionId.ToSHA1()}";
                allHeartbeatKeys.Add(sessionIdKey);
            }

            var user = session.GetUserIdentity(_jsonOptions);
            if (!String.IsNullOrWhiteSpace(user?.Identity))
            {
                userIdentityKey = $"Project:{session.ProjectId}:heartbeat:{user.Identity.ToSHA1()}";
                allHeartbeatKeys.Add(userIdentityKey);
            }

            sessionKeyMap[i] = (sessionIdKey, userIdentityKey);
        }

        if (allHeartbeatKeys.Count == 0)
            return new HeartbeatResult?[sessions.Count];

        var heartbeatValues = await _cache.GetAllAsync<DateTime>(allHeartbeatKeys);

        var closeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new (DateTime ActivityUtc, string CacheKey)?[sessions.Count];

        for (int i = 0; i < sessionKeyMap.Length; i++)
        {
            var (sessionIdKey, userIdentityKey) = sessionKeyMap[i];
            string? matchedKey = null;
            DateTime activityUtc = default;

            if (sessionIdKey is not null && heartbeatValues.TryGetValue(sessionIdKey, out var sidVal) && sidVal.HasValue)
            {
                matchedKey = sessionIdKey;
                activityUtc = sidVal.Value;
            }
            else if (userIdentityKey is not null && heartbeatValues.TryGetValue(userIdentityKey, out var uidVal) && uidVal.HasValue)
            {
                matchedKey = userIdentityKey;
                activityUtc = uidVal.Value;
            }

            if (matchedKey is not null)
            {
                resolved[i] = (activityUtc, matchedKey);
                closeKeys.Add($"{matchedKey}-close");
            }
        }

        IDictionary<string, CacheValue<bool>> closeValues = closeKeys.Count > 0
            ? await _cache.GetAllAsync<bool>(closeKeys)
            : new Dictionary<string, CacheValue<bool>>();

        var results = new HeartbeatResult?[sessions.Count];
        for (int i = 0; i < resolved.Length; i++)
        {
            if (resolved[i] is not { } r)
                continue;

            bool close = closeValues.TryGetValue($"{r.CacheKey}-close", out var closeVal) && closeVal.HasValue && closeVal.Value;
            results[i] = new HeartbeatResult { ActivityUtc = r.ActivityUtc, Close = close, CacheKey = r.CacheKey };
        }

        return results;
    }

    public TimeSpan DefaultInactivePeriod { get; set; } = TimeSpan.FromMinutes(5);

    private record HeartbeatResult
    {
        public required DateTime ActivityUtc { get; init; }
        public required string CacheKey { get; init; }
        public required bool Close { get; init; }
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastActivity.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastActivity.Value) > TimeSpan.FromMinutes(5))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has no activity in the last 5 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has no activity in the last 5 minutes."));
    }
}
