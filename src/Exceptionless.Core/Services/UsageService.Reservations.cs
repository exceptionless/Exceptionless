using System.Globalization;
using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Lock;

namespace Exceptionless.Core.Services;

public partial class UsageService
{
    private static readonly TimeSpan IngestReservationLockLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IngestReservationRetention = TimeSpan.FromDays(35);

    public async Task<EventIngestReservation> ReserveEventIngestAsync(Organization organization, Project project, string reservationId,
        IReadOnlyCollection<EventIngestCandidate> candidates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(reservationId);
        if (!String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal))
            throw new ArgumentException("The project does not belong to the organization.", nameof(project));

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        int maxEventsPerMonth = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
        int effectiveProjectLimit = GetEffectiveProjectLimit(project, maxEventsPerMonth);

        // This is the common self-hosted/unlimited path. It must not take the organization hot-path lock.
        if (maxEventsPerMonth < 0 && effectiveProjectLimit < 0)
            return EventIngestReservation.Unlimited(reservationId, organization.Id, project.Id, GetTotalBucket(utcNow), utcNow.Floor(_bucketSize), candidates);

        SmartThrottleResult? smartThrottleToActivate = null;
        EventIngestReservation reservation;
        await using (await _lockProvider.AcquireAsync(GetIngestReservationLockKey(organization.Id), IngestReservationLockLifetime, cancellationToken))
        {
            string reservationKey = GetIngestReservationKey(organization.Id, reservationId);
            var existingValue = await _cache.GetAsync<string>(reservationKey);
            if (existingValue.HasValue)
            {
                var existing = DeserializeReservation(existingValue.Value);
                if (existing.State is EventIngestReservationState.Active or EventIngestReservationState.Completed)
                    return existing.ToReservation();
            }

            int[] recentUsagePeriods = GetRecentUsagePeriods(utcNow);
            string[] organizationReservedKeys = recentUsagePeriods.Select(period => GetIngestReservedKey(period, organization.Id)).ToArray();
            string[] projectReservedKeys = recentUsagePeriods.Select(period => GetIngestReservedKey(period, organization.Id, project.Id)).ToArray();
            var reservedValues = await _cache.GetAllAsync<string>(organizationReservedKeys.Concat(projectReservedKeys));
            int organizationReserved = organizationReservedKeys.Sum(key => GetCachedInt(reservedValues, key));
            int projectReserved = projectReservedKeys.Sum(key => GetCachedInt(reservedValues, key));
            string organizationReservedKey = organizationReservedKeys[0];
            string projectReservedKey = projectReservedKeys[0];
            int currentOrganizationReserved = GetCachedInt(reservedValues, organizationReservedKey);
            int currentProjectReserved = GetCachedInt(reservedValues, projectReservedKey);
            var totals = await GetAcceptedUsageTotalsAsync(utcNow, organization, project);
            totals = totals with
            {
                OrganizationTotal = checked(totals.OrganizationTotal + organizationReserved),
                ProjectTotal = checked(totals.ProjectTotal + projectReserved),
                // Every outstanding reservation counts against the active bucket. This is deliberately
                // conservative and closes both five-minute and monthly rollover races.
                OrganizationCurrentBucket = checked(totals.OrganizationCurrentBucket + organizationReserved),
                ProjectCurrentBucket = checked(totals.ProjectCurrentBucket + projectReserved)
            };

            var allowance = await CalculateEventIngestAllowanceAsync(utcNow, organization, project, maxEventsPerMonth,
                effectiveProjectLimit, totals, candidates.Count, false);
            var selectedCandidates = candidates;
            int smartThrottleBlockedCount = 0;
            if (allowance.SmartThrottle.IsThrottled)
            {
                int sampleThreshold = (int)(allowance.SmartThrottle.SampleRate * 10_000);
                selectedCandidates = candidates.Where(candidate => candidate.Hash % 10_000 < (ulong)sampleThreshold).ToArray();
                smartThrottleBlockedCount = candidates.Count - selectedCandidates.Count;
            }

            if (selectedCandidates.Count > allowance.EventsLeft)
                selectedCandidates = selectedCandidates.OrderBy(candidate => candidate.Hash).Take(allowance.EventsLeft).ToArray();

            int[] acceptedIndexes = selectedCandidates.OrderBy(candidate => candidate.Index).Select(candidate => candidate.Index).ToArray();
            var smartThrottle = allowance.SmartThrottle.IsThrottled
                ? new SmartThrottleResult
                {
                    IsThrottled = true,
                    SampleRate = allowance.SmartThrottle.SampleRate,
                    ProjectShare = (double)(totals.ProjectTotal + acceptedIndexes.Length) / Math.Max(1, totals.OrganizationTotal + acceptedIndexes.Length),
                    FairShareRatio = allowance.SmartThrottle.FairShareRatio,
                    CurrentProjectUsage = totals.ProjectTotal + acceptedIndexes.Length,
                    FairShareLimit = allowance.SmartThrottle.FairShareLimit
                }
                : SmartThrottleResult.NoThrottle;

            var record = new EventIngestReservationRecord(
                reservationId,
                organization.Id,
                project.Id,
                GetTotalBucket(utcNow),
                utcNow.Floor(_bucketSize),
                acceptedIndexes,
                smartThrottleBlockedCount,
                smartThrottle,
                EventIngestReservationState.Active,
                0,
                null,
                0);

            var updatedValues = new Dictionary<string, string>
            {
                [reservationKey] = JsonSerializer.Serialize(record),
                [organizationReservedKey] = FormatInt(checked(currentOrganizationReserved + record.ReservedCount)),
                [projectReservedKey] = FormatInt(checked(currentProjectReserved + record.ReservedCount))
            };
            var expectedValues = new Dictionary<string, string?>
            {
                [reservationKey] = existingValue.HasValue ? existingValue.Value : null,
                [organizationReservedKey] = GetCachedString(reservedValues, organizationReservedKey),
                [projectReservedKey] = GetCachedString(reservedValues, projectReservedKey)
            };
            if (!await _atomicCacheBatch.TrySetAllAsync(expectedValues, updatedValues, IngestReservationRetention))
                throw new UsageServiceException($"Ingest reservation '{reservationId}' changed while it was being reserved; retry the queue entry.");

            reservation = record.ToReservation();
            if (smartThrottle.IsThrottled)
                smartThrottleToActivate = smartThrottle;
        }

        if (smartThrottleToActivate is not null)
            await ActivateSmartThrottleAsync(utcNow, organization, project, smartThrottleToActivate);

        return reservation;
    }

    public async Task CompleteEventIngestReservationAsync(EventIngestReservation reservation, Organization organization, int processedCount)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(organization);
        if (processedCount is < 0 || processedCount > reservation.ReservedCount)
            throw new ArgumentOutOfRangeException(nameof(processedCount));
        if (!reservation.IsTracked)
        {
            await IncrementTotalAsync(organization, reservation.ProjectId, processedCount);
            return;
        }

        DateTime completionBucketUtc;
        long organizationBucketTotal;
        bool alreadyCompleted;
        await using (await _lockProvider.AcquireAsync(GetIngestReservationLockKey(reservation.OrganizationId), IngestReservationLockLifetime, CancellationToken.None))
        {
            string reservationKey = GetIngestReservationKey(reservation.OrganizationId, reservation.Id);
            var recordValue = await _cache.GetAsync<string>(reservationKey);
            if (!recordValue.HasValue)
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' is missing and cannot be completed safely.");

            var record = DeserializeReservation(recordValue.Value);
            alreadyCompleted = record.State is EventIngestReservationState.Completed;
            if (alreadyCompleted)
            {
                completionBucketUtc = record.CompletionBucketUtc ?? throw new UsageServiceException($"Completed ingest reservation '{reservation.Id}' is missing its completion bucket.");
                organizationBucketTotal = record.CompletionOrganizationBucketTotal;
            }
            else
            {
                if (record.State is not EventIngestReservationState.Active)
                    throw new UsageServiceException($"Ingest reservation '{reservation.Id}' is not active and cannot be completed.");

                completionBucketUtc = _timeProvider.GetUtcNow().UtcDateTime.Floor(_bucketSize);
                string organizationReservedKey = GetIngestReservedKey(reservation.UsagePeriod, reservation.OrganizationId);
                string projectReservedKey = GetIngestReservedKey(reservation.UsagePeriod, reservation.OrganizationId, reservation.ProjectId);
                string organizationBucketKey = GetBucketTotalCacheKey(completionBucketUtc, reservation.OrganizationId);
                string projectBucketKey = GetBucketTotalCacheKey(completionBucketUtc, reservation.OrganizationId, reservation.ProjectId);
                string[] keys = [reservationKey, organizationReservedKey, projectReservedKey, organizationBucketKey, projectBucketKey];
                var values = await _cache.GetAllAsync<string>(keys);
                organizationBucketTotal = checked(GetCachedInt(values, organizationBucketKey) + processedCount);
                var updatedValues = new Dictionary<string, string>
                {
                    [reservationKey] = JsonSerializer.Serialize(record with
                    {
                        State = EventIngestReservationState.Completed,
                        ProcessedCount = processedCount,
                        CompletionBucketUtc = completionBucketUtc,
                        CompletionOrganizationBucketTotal = organizationBucketTotal
                    }),
                    [organizationReservedKey] = FormatInt(Math.Max(0, GetCachedInt(values, organizationReservedKey) - record.ReservedCount)),
                    [projectReservedKey] = FormatInt(Math.Max(0, GetCachedInt(values, projectReservedKey) - record.ReservedCount)),
                    [organizationBucketKey] = FormatInt(organizationBucketTotal),
                    [projectBucketKey] = FormatInt(checked(GetCachedInt(values, projectBucketKey) + processedCount))
                };
                var usageSetMembers = new Dictionary<string, string>
                {
                    [GetOrganizationSetKey(completionBucketUtc)] = reservation.OrganizationId,
                    [GetProjectSetKey(completionBucketUtc)] = reservation.ProjectId
                };
                if (!await _atomicCacheBatch.TrySetAllAsync(GetExpectedValues(values, keys), updatedValues, IngestReservationRetention, usageSetMembers, TimeSpan.FromHours(8)))
                    throw new UsageServiceException($"Ingest reservation '{reservation.Id}' changed while it was being completed; retry the queue entry.");
            }
        }

        int committedCount = alreadyCompleted ? reservation.ProcessedCount : processedCount;
        if (committedCount > 0)
            await PublishUsageIncrementNotificationsAsync(organization, reservation.ProjectId, committedCount, completionBucketUtc, organizationBucketTotal);
    }

    public async Task ReleaseEventIngestReservationAsync(EventIngestReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!reservation.IsTracked || reservation.ReservedCount == 0 || reservation.IsCompleted)
            return;

        await using (await _lockProvider.AcquireAsync(GetIngestReservationLockKey(reservation.OrganizationId), IngestReservationLockLifetime, CancellationToken.None))
        {
            string reservationKey = GetIngestReservationKey(reservation.OrganizationId, reservation.Id);
            string organizationReservedKey = GetIngestReservedKey(reservation.UsagePeriod, reservation.OrganizationId);
            string projectReservedKey = GetIngestReservedKey(reservation.UsagePeriod, reservation.OrganizationId, reservation.ProjectId);
            string[] keys = [reservationKey, organizationReservedKey, projectReservedKey];
            var values = await _cache.GetAllAsync<string>(keys);
            if (!values.TryGetValue(reservationKey, out var recordValue) || !recordValue.HasValue)
                return;

            var record = DeserializeReservation(recordValue.Value);
            if (record.State is EventIngestReservationState.Released or EventIngestReservationState.Completed)
                return;

            var updatedValues = new Dictionary<string, string>
            {
                [reservationKey] = JsonSerializer.Serialize(record with { State = EventIngestReservationState.Released }),
                [organizationReservedKey] = FormatInt(Math.Max(0, GetCachedInt(values, organizationReservedKey) - record.ReservedCount)),
                [projectReservedKey] = FormatInt(Math.Max(0, GetCachedInt(values, projectReservedKey) - record.ReservedCount))
            };
            if (!await _atomicCacheBatch.TrySetAllAsync(GetExpectedValues(values, keys), updatedValues, IngestReservationRetention))
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' changed while it was being released; retry the queue entry.");
        }
    }

    private static EventIngestReservationRecord DeserializeReservation(string value) =>
        JsonSerializer.Deserialize<EventIngestReservationRecord>(value) ?? throw new UsageServiceException("Invalid ingest reservation state.");

    private static int GetCachedInt(IDictionary<string, Foundatio.Caching.CacheValue<string>> values, string key) =>
        values.TryGetValue(key, out var value) && value.HasValue && Int32.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;

    private static string? GetCachedString(IDictionary<string, Foundatio.Caching.CacheValue<string>> values, string key) =>
        values.TryGetValue(key, out var value) && value.HasValue ? value.Value : null;

    private static Dictionary<string, string?> GetExpectedValues(IDictionary<string, Foundatio.Caching.CacheValue<string>> values, IEnumerable<string> keys) =>
        keys.ToDictionary(key => key, key => GetCachedString(values, key));

    private static string FormatInt(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static int[] GetRecentUsagePeriods(DateTime utcNow)
    {
        var periodStart = utcNow.StartOfMonth();
        return [periodStart.ToEpoch(), periodStart.AddMonths(-1).ToEpoch(), periodStart.AddMonths(-2).ToEpoch()];
    }

    private static string GetIngestReservedKey(int usagePeriod, string organizationId, string? projectId = null) =>
        String.IsNullOrEmpty(projectId) ? $"usage:reserved:{{{organizationId}}}:{usagePeriod}:total" : $"usage:reserved:{{{organizationId}}}:{usagePeriod}:{projectId}:total";
    private static string GetIngestReservationKey(string organizationId, string reservationId) => $"usage:ingest-reservation:{{{organizationId}}}:{reservationId}";
    private static string GetIngestReservationLockKey(string organizationId) => $"usage:ingest-reservation-lock:{organizationId}";

    private sealed record EventIngestReservationRecord(
        string Id,
        string OrganizationId,
        string ProjectId,
        int UsagePeriod,
        DateTime BucketUtc,
        int[] AcceptedIndexes,
        int SmartThrottleBlockedCount,
        SmartThrottleResult SmartThrottle,
        EventIngestReservationState State,
        int ProcessedCount,
        DateTime? CompletionBucketUtc,
        long CompletionOrganizationBucketTotal)
    {
        public int ReservedCount => AcceptedIndexes.Length;

        public EventIngestReservation ToReservation() => new(
            Id,
            OrganizationId,
            ProjectId,
            UsagePeriod,
            BucketUtc,
            AcceptedIndexes,
            SmartThrottleBlockedCount,
            SmartThrottle,
            true,
            State is EventIngestReservationState.Completed,
            ProcessedCount,
            CompletionBucketUtc);
    }

    private enum EventIngestReservationState
    {
        Active,
        Completed,
        Released
    }
}

public sealed record EventIngestCandidate(int Index, ulong Hash);

public sealed record EventIngestReservation(
    string Id,
    string OrganizationId,
    string ProjectId,
    int UsagePeriod,
    DateTime BucketUtc,
    int[] AcceptedIndexes,
    int SmartThrottleBlockedCount,
    SmartThrottleResult SmartThrottle,
    bool IsTracked,
    bool IsCompleted,
    int ProcessedCount,
    DateTime? CompletionBucketUtc)
{
    public int ReservedCount => AcceptedIndexes.Length;

    public static EventIngestReservation Unlimited(string id, string organizationId, string projectId, int usagePeriod, DateTime bucketUtc,
        IReadOnlyCollection<EventIngestCandidate> candidates) => new(
        id,
        organizationId,
        projectId,
        usagePeriod,
        bucketUtc,
        candidates.OrderBy(candidate => candidate.Index).Select(candidate => candidate.Index).ToArray(),
        0,
        SmartThrottleResult.NoThrottle,
        false,
        false,
        0,
        null);
}
