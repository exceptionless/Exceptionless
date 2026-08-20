using System.Collections.Immutable;
using System.Diagnostics;
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

    internal async Task<EventIngestReservation> ReserveEventIngestAsync(Organization organization, Project project, string reservationId,
        IReadOnlyCollection<EventIngestCandidate> candidates, CancellationToken cancellationToken = default)
    {
        using var reservationTimer = AppDiagnostics.IngestReservationReserveTime.StartTimer();
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
        int newlyBlockedCount = 0;
        int newlySmartThrottleBlockedCount = 0;
        var lockWait = Stopwatch.StartNew();
        await using (await _lockProvider.AcquireAsync(GetIngestReservationLockKey(organization.Id), IngestReservationLockLifetime, cancellationToken))
        {
            AppDiagnostics.IngestReservationLockWait.Record(lockWait.Elapsed.TotalMilliseconds);
            string reservationKey = GetIngestReservationKey(organization.Id, reservationId);
            var existingValue = await _cache.GetAsync<string>(reservationKey);
            EventIngestReservationRecord? existing = existingValue.HasValue
                ? DeserializeReservation(existingValue.Value, candidates.Count)
                : null;
            if (existing is not null && existing.SubmittedCount != candidates.Count)
                throw new UsageServiceException($"Ingest reservation '{reservationId}' was retried with a different event count.");

            if (existing?.State is EventIngestReservationState.Active or EventIngestReservationState.Completed)
            {
                reservation = existing.ToReservation();
                if (existing.State is EventIngestReservationState.Active && existing.SmartThrottle.IsThrottled)
                    smartThrottleToActivate = existing.SmartThrottle;
            }
            else
            {
                var creation = await CreateTrackedReservationAsync(
                    utcNow,
                    organization,
                    project,
                    reservationId,
                    candidates,
                    maxEventsPerMonth,
                    effectiveProjectLimit,
                    reservationKey,
                    existingValue.HasValue ? existingValue.Value : null,
                    existing);
                reservation = creation.Record.ToReservation();
                newlyBlockedCount = creation.NewlyBlockedCount;
                newlySmartThrottleBlockedCount = creation.NewlySmartThrottleBlockedCount;
                if (creation.Record.SmartThrottle.IsThrottled)
                    smartThrottleToActivate = creation.Record.SmartThrottle;
            }
        }

        if (newlyBlockedCount > 0)
            AppDiagnostics.EventsBlocked.Add(newlyBlockedCount);
        if (newlySmartThrottleBlockedCount > 0)
            AppDiagnostics.EventsSmartThrottled.Add(newlySmartThrottleBlockedCount);
        if (smartThrottleToActivate is not null)
            await ActivateSmartThrottleAsync(utcNow, organization, project, smartThrottleToActivate);

        return reservation;
    }

    private async Task<ReservationCreationResult> CreateTrackedReservationAsync(
        DateTime utcNow,
        Organization organization,
        Project project,
        string reservationId,
        IReadOnlyCollection<EventIngestCandidate> candidates,
        int maxEventsPerMonth,
        int effectiveProjectLimit,
        string reservationKey,
        string? existingReservationValue,
        EventIngestReservationRecord? previous)
    {
        IReadOnlyCollection<EventIngestCandidate> eligibleCandidates = candidates;
        int previousBlockedCount = 0;
        if (previous is not null)
        {
            if (previous.State is not EventIngestReservationState.Released)
                throw new UsageServiceException($"Ingest reservation '{reservationId}' has an invalid state.");
            if (previous.SubmittedCount != candidates.Count)
                throw new UsageServiceException($"Ingest reservation '{reservationId}' was retried with a different event count.");

            var previouslyAcceptedIndexes = previous.AcceptedIndexes.ToHashSet();
            eligibleCandidates = candidates.Where(candidate => previouslyAcceptedIndexes.Contains(candidate.Index)).ToArray();
            previousBlockedCount = previous.BlockedCount;
        }

        int[] recentUsagePeriods = GetRecentUsagePeriods(utcNow);
        string[] organizationReservedKeys = recentUsagePeriods.Select(period => GetIngestReservedKey(period, organization.Id)).ToArray();
        string[] projectReservedKeys = recentUsagePeriods.Select(period => GetIngestReservedKey(period, organization.Id, project.Id)).ToArray();
        string organizationBlockedKey = GetBucketBlockedCacheKey(utcNow, organization.Id);
        string projectBlockedKey = GetBucketBlockedCacheKey(utcNow, organization.Id, project.Id);
        string[] ledgerKeys = organizationReservedKeys
            .Concat(projectReservedKeys)
            .Append(organizationBlockedKey)
            .Append(projectBlockedKey)
            .ToArray();
        var ledgerValues = await _cache.GetAllAsync<string>(ledgerKeys);
        int organizationReserved = organizationReservedKeys.Sum(key => GetLedgerInt(ledgerValues, key));
        int projectReserved = projectReservedKeys.Sum(key => GetLedgerInt(ledgerValues, key));
        string organizationReservedKey = organizationReservedKeys[0];
        string projectReservedKey = projectReservedKeys[0];
        int currentOrganizationReserved = GetLedgerInt(ledgerValues, organizationReservedKey);
        int currentProjectReserved = GetLedgerInt(ledgerValues, projectReservedKey);
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
            effectiveProjectLimit, totals, eligibleCandidates.Count, false);
        var selectedCandidates = eligibleCandidates;
        int smartThrottleBlockedCount = 0;
        if (allowance.SmartThrottle.IsThrottled)
        {
            int sampleThreshold = (int)(allowance.SmartThrottle.SampleRate * 10_000);
            selectedCandidates = eligibleCandidates.Where(candidate => candidate.Hash % 10_000 < (ulong)sampleThreshold).ToArray();
            smartThrottleBlockedCount = eligibleCandidates.Count - selectedCandidates.Count;
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
            Guid.NewGuid(),
            organization.Id,
            project.Id,
            GetTotalBucket(utcNow),
            utcNow.Floor(_bucketSize),
            candidates.Count,
            acceptedIndexes,
            smartThrottle,
            EventIngestReservationState.Active,
            0,
            null,
            0);
        int newlyBlockedCount = checked(record.BlockedCount - previousBlockedCount);
        if (newlyBlockedCount < 0)
            throw new UsageServiceException($"Ingest reservation '{reservationId}' cannot accept events that were previously blocked.");

        string serializedRecord = JsonSerializer.Serialize(record);
        AppDiagnostics.IngestReservationRecordSize.Record(serializedRecord.Length);
        var updatedValues = new Dictionary<string, AtomicCacheValue>
        {
            [reservationKey] = new(serializedRecord, IngestReservationRetention),
            [organizationReservedKey] = new(FormatInt(checked(currentOrganizationReserved + record.ReservedCount)), IngestReservationRetention),
            [projectReservedKey] = new(FormatInt(checked(currentProjectReserved + record.ReservedCount)), IngestReservationRetention)
        };
        var expectedValues = new Dictionary<string, string?>
        {
            [reservationKey] = existingReservationValue,
            [organizationReservedKey] = GetCachedString(ledgerValues, organizationReservedKey),
            [projectReservedKey] = GetCachedString(ledgerValues, projectReservedKey)
        };
        Dictionary<string, string>? usageSetMembers = null;
        if (newlyBlockedCount > 0)
        {
            updatedValues[organizationBlockedKey] = new(
                FormatInt(checked(GetLedgerInt(ledgerValues, organizationBlockedKey) + newlyBlockedCount)),
                TimeSpan.FromHours(8));
            updatedValues[projectBlockedKey] = new(
                FormatInt(checked(GetLedgerInt(ledgerValues, projectBlockedKey) + newlyBlockedCount)),
                TimeSpan.FromHours(8));
            expectedValues[organizationBlockedKey] = GetCachedString(ledgerValues, organizationBlockedKey);
            expectedValues[projectBlockedKey] = GetCachedString(ledgerValues, projectBlockedKey);
            usageSetMembers = new Dictionary<string, string>
            {
                [GetOrganizationSetKey(utcNow)] = organization.Id,
                [GetProjectSetKey(utcNow)] = project.Id
            };
        }

        if (!await _atomicCacheBatch.TrySetAllAsync(expectedValues, updatedValues, usageSetMembers, TimeSpan.FromHours(8)))
        {
            AppDiagnostics.IngestReservationCasConflicts.Add(1);
            throw new UsageServiceException($"Ingest reservation '{reservationId}' changed while it was being reserved; retry the queue entry.");
        }

        return new ReservationCreationResult(record, newlyBlockedCount, smartThrottleBlockedCount);
    }

    internal async Task CompleteEventIngestReservationAsync(EventIngestReservation reservation, Organization organization, int processedCount)
    {
        using var reservationTimer = AppDiagnostics.IngestReservationCompleteTime.StartTimer();
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(organization);
        if (!reservation.IsTracked)
        {
            if (!String.Equals(reservation.OrganizationId, organization.Id, StringComparison.Ordinal))
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' does not belong to organization '{organization.Id}'.");
            if (processedCount is < 0 || processedCount > reservation.ReservedCount)
                throw new ArgumentOutOfRangeException(nameof(processedCount));
            await IncrementTotalAsync(organization, reservation.ProjectId, processedCount);
            return;
        }

        DateTime completionBucketUtc;
        long organizationBucketTotal;
        int committedCount;
        string projectId;
        var lockWait = Stopwatch.StartNew();
        await using (await _lockProvider.AcquireAsync(GetIngestReservationLockKey(reservation.OrganizationId), IngestReservationLockLifetime, CancellationToken.None))
        {
            AppDiagnostics.IngestReservationLockWait.Record(lockWait.Elapsed.TotalMilliseconds);
            string reservationKey = GetIngestReservationKey(reservation.OrganizationId, reservation.Id);
            var recordValue = await _cache.GetAsync<string>(reservationKey);
            if (!recordValue.HasValue)
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' is missing and cannot be completed safely.");

            var record = DeserializeReservation(recordValue.Value);
            ValidateReservationIdentity(record, reservation, organization.Id);
            projectId = record.ProjectId;
            if (record.State is EventIngestReservationState.Completed)
            {
                completionBucketUtc = record.CompletionBucketUtc ?? throw new UsageServiceException($"Completed ingest reservation '{reservation.Id}' is missing its completion bucket.");
                organizationBucketTotal = record.CompletionOrganizationBucketTotal;
                committedCount = record.ProcessedCount;
            }
            else
            {
                if (record.State is not EventIngestReservationState.Active)
                    throw new UsageServiceException($"Ingest reservation '{reservation.Id}' is not active and cannot be completed.");
                if (processedCount is < 0 || processedCount > record.ReservedCount)
                    throw new ArgumentOutOfRangeException(nameof(processedCount));

                completionBucketUtc = _timeProvider.GetUtcNow().UtcDateTime.Floor(_bucketSize);
                string organizationReservedKey = GetIngestReservedKey(record.UsagePeriod, record.OrganizationId);
                string projectReservedKey = GetIngestReservedKey(record.UsagePeriod, record.OrganizationId, record.ProjectId);
                string organizationBucketKey = GetBucketTotalCacheKey(completionBucketUtc, record.OrganizationId);
                string projectBucketKey = GetBucketTotalCacheKey(completionBucketUtc, record.OrganizationId, record.ProjectId);
                string[] keys = [reservationKey, organizationReservedKey, projectReservedKey, organizationBucketKey, projectBucketKey];
                var values = await _cache.GetAllAsync<string>(keys);
                int organizationReserved = GetLedgerInt(values, organizationReservedKey);
                int projectReserved = GetLedgerInt(values, projectReservedKey);
                if (organizationReserved < record.ReservedCount || projectReserved < record.ReservedCount)
                    throw new UsageServiceException($"Ingest reservation '{reservation.Id}' counters are lower than its reserved event count.");

                organizationBucketTotal = checked(GetLedgerInt(values, organizationBucketKey) + processedCount);
                record = record with
                {
                    State = EventIngestReservationState.Completed,
                    ProcessedCount = processedCount,
                    CompletionBucketUtc = completionBucketUtc,
                    CompletionOrganizationBucketTotal = organizationBucketTotal
                };
                string serializedRecord = JsonSerializer.Serialize(record);
                AppDiagnostics.IngestReservationRecordSize.Record(serializedRecord.Length);
                var updatedValues = new Dictionary<string, AtomicCacheValue>
                {
                    [reservationKey] = new(serializedRecord, IngestReservationRetention),
                    [organizationReservedKey] = new(FormatInt(organizationReserved - record.ReservedCount), IngestReservationRetention),
                    [projectReservedKey] = new(FormatInt(projectReserved - record.ReservedCount), IngestReservationRetention),
                    [organizationBucketKey] = new(FormatInt(organizationBucketTotal), TimeSpan.FromHours(8)),
                    [projectBucketKey] = new(FormatInt(checked(GetLedgerInt(values, projectBucketKey) + processedCount)), TimeSpan.FromHours(8))
                };
                var usageSetMembers = new Dictionary<string, string>
                {
                    [GetOrganizationSetKey(completionBucketUtc)] = record.OrganizationId,
                    [GetProjectSetKey(completionBucketUtc)] = record.ProjectId
                };
                if (!await _atomicCacheBatch.TrySetAllAsync(GetExpectedValues(values, keys), updatedValues, usageSetMembers, TimeSpan.FromHours(8)))
                {
                    AppDiagnostics.IngestReservationCasConflicts.Add(1);
                    throw new UsageServiceException($"Ingest reservation '{reservation.Id}' changed while it was being completed; retry the queue entry.");
                }

                committedCount = processedCount;
            }
        }

        if (committedCount > 0)
            await PublishUsageIncrementNotificationsAsync(organization, projectId, committedCount, completionBucketUtc, organizationBucketTotal);
    }

    internal async Task ReleaseEventIngestReservationAsync(EventIngestReservation reservation)
    {
        using var reservationTimer = AppDiagnostics.IngestReservationReleaseTime.StartTimer();
        ArgumentNullException.ThrowIfNull(reservation);
        if (!reservation.IsTracked)
            return;

        var lockWait = Stopwatch.StartNew();
        await using (await _lockProvider.AcquireAsync(GetIngestReservationLockKey(reservation.OrganizationId), IngestReservationLockLifetime, CancellationToken.None))
        {
            AppDiagnostics.IngestReservationLockWait.Record(lockWait.Elapsed.TotalMilliseconds);
            string reservationKey = GetIngestReservationKey(reservation.OrganizationId, reservation.Id);
            var recordValue = await _cache.GetAsync<string>(reservationKey);
            if (!recordValue.HasValue)
                return;

            var record = DeserializeReservation(recordValue.Value);
            ValidateReservationIdentity(record, reservation);
            if (record.State is EventIngestReservationState.Released or EventIngestReservationState.Completed)
                return;
            if (record.State is not EventIngestReservationState.Active)
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' is not active and cannot be released.");

            string organizationReservedKey = GetIngestReservedKey(record.UsagePeriod, record.OrganizationId);
            string projectReservedKey = GetIngestReservedKey(record.UsagePeriod, record.OrganizationId, record.ProjectId);
            string[] keys = [reservationKey, organizationReservedKey, projectReservedKey];
            var values = await _cache.GetAllAsync<string>(keys);
            int organizationReserved = GetLedgerInt(values, organizationReservedKey);
            int projectReserved = GetLedgerInt(values, projectReservedKey);
            if (organizationReserved < record.ReservedCount || projectReserved < record.ReservedCount)
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' counters are lower than its reserved event count.");

            string serializedRecord = JsonSerializer.Serialize(record with { State = EventIngestReservationState.Released });
            AppDiagnostics.IngestReservationRecordSize.Record(serializedRecord.Length);
            var updatedValues = new Dictionary<string, AtomicCacheValue>
            {
                [reservationKey] = new(serializedRecord, IngestReservationRetention),
                [organizationReservedKey] = new(FormatInt(organizationReserved - record.ReservedCount), IngestReservationRetention),
                [projectReservedKey] = new(FormatInt(projectReserved - record.ReservedCount), IngestReservationRetention)
            };
            if (!await _atomicCacheBatch.TrySetAllAsync(GetExpectedValues(values, keys), updatedValues))
            {
                AppDiagnostics.IngestReservationCasConflicts.Add(1);
                throw new UsageServiceException($"Ingest reservation '{reservation.Id}' changed while it was being released; retry the queue entry.");
            }
        }
    }

    private static EventIngestReservationRecord DeserializeReservation(string value, int? submittedCount = null)
    {
        var record = JsonSerializer.Deserialize<EventIngestReservationRecord>(value) ?? throw new UsageServiceException("Invalid ingest reservation state.");
        if (record.AcceptedIndexes is null || record.SmartThrottle is null)
            throw new UsageServiceException($"Ingest reservation '{record.Id}' contains invalid state.");

        int normalizedSubmittedCount = record.SubmittedCount;
        if (normalizedSubmittedCount == 0 && submittedCount.HasValue)
            normalizedSubmittedCount = submittedCount.Value;
        else if (normalizedSubmittedCount == 0 && record.AcceptedIndexes.Length > 0)
            normalizedSubmittedCount = checked(record.AcceptedIndexes.Max() + 1);
        if (normalizedSubmittedCount != record.SubmittedCount)
            record = record with { SubmittedCount = normalizedSubmittedCount };

        if (String.IsNullOrEmpty(record.Id) || String.IsNullOrEmpty(record.OrganizationId) || String.IsNullOrEmpty(record.ProjectId))
            throw new UsageServiceException("Ingest reservation identity is invalid.");
        if (record.SubmittedCount < 0
            || record.AcceptedIndexes.Any(index => index < 0 || index >= record.SubmittedCount)
            || !record.AcceptedIndexes.SequenceEqual(record.AcceptedIndexes.Distinct().Order()))
            throw new UsageServiceException($"Ingest reservation '{record.Id}' contains invalid accepted indexes.");
        if (!Enum.IsDefined(record.State)
            || record.ProcessedCount < 0
            || record.ProcessedCount > record.ReservedCount
            || record.CompletionOrganizationBucketTotal < 0)
            throw new UsageServiceException($"Ingest reservation '{record.Id}' contains invalid transition state.");
        if (record.State is EventIngestReservationState.Completed && record.CompletionBucketUtc is null)
            throw new UsageServiceException($"Completed ingest reservation '{record.Id}' is missing its completion bucket.");

        return record;
    }

    private static int GetLedgerInt(IDictionary<string, Foundatio.Caching.CacheValue<string>> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || !value.HasValue)
            return 0;
        if (!Int32.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result < 0)
            throw new UsageServiceException($"Usage ledger counter '{key}' is invalid.");

        return result;
    }

    private static void ValidateReservationIdentity(EventIngestReservationRecord record, EventIngestReservation reservation, string? organizationId = null)
    {
        if (!String.Equals(record.Id, reservation.Id, StringComparison.Ordinal)
            || !String.Equals(record.OrganizationId, reservation.OrganizationId, StringComparison.Ordinal)
            || !String.Equals(record.ProjectId, reservation.ProjectId, StringComparison.Ordinal)
            || record.UsagePeriod != reservation.UsagePeriod
            || record.GenerationId != reservation.GenerationId)
            throw new UsageServiceException($"Ingest reservation '{reservation.Id}' no longer matches its persisted generation.");
        if (organizationId is not null && !String.Equals(record.OrganizationId, organizationId, StringComparison.Ordinal))
            throw new UsageServiceException($"Ingest reservation '{reservation.Id}' does not belong to organization '{organizationId}'.");
    }

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
        Guid GenerationId,
        string OrganizationId,
        string ProjectId,
        int UsagePeriod,
        DateTime BucketUtc,
        int SubmittedCount,
        int[] AcceptedIndexes,
        SmartThrottleResult SmartThrottle,
        EventIngestReservationState State,
        int ProcessedCount,
        DateTime? CompletionBucketUtc,
        long CompletionOrganizationBucketTotal)
    {
        public int ReservedCount => AcceptedIndexes.Length;
        public int BlockedCount => SubmittedCount - ReservedCount;

        public EventIngestReservation ToReservation() => new(
            Id,
            GenerationId,
            OrganizationId,
            ProjectId,
            UsagePeriod,
            BucketUtc,
            AcceptedIndexes.ToImmutableArray(),
            SmartThrottle,
            true,
            State is EventIngestReservationState.Completed,
            ProcessedCount,
            CompletionBucketUtc);
    }

    private sealed record ReservationCreationResult(EventIngestReservationRecord Record, int NewlyBlockedCount, int NewlySmartThrottleBlockedCount);

    private enum EventIngestReservationState
    {
        Active,
        Completed,
        Released
    }
}

internal readonly record struct EventIngestCandidate(int Index, ulong Hash);

internal sealed record EventIngestReservation(
    string Id,
    Guid GenerationId,
    string OrganizationId,
    string ProjectId,
    int UsagePeriod,
    DateTime BucketUtc,
    ImmutableArray<int> AcceptedIndexes,
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
        Guid.Empty,
        organizationId,
        projectId,
        usagePeriod,
        bucketUtc,
        candidates.OrderBy(candidate => candidate.Index).Select(candidate => candidate.Index).ToImmutableArray(),
        SmartThrottleResult.NoThrottle,
        false,
        false,
        0,
        null);
}
