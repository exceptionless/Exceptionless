using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services;

public interface IEventBatchWriter
{
    Task<IReadOnlyList<EventIngestionIdentity>> PrepareAsync(
        IReadOnlyCollection<EventIngestionV3Event> events,
        string projectId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task ReconcileAsync(
        IReadOnlyCollection<EventIngestionReconciliation> reconciliations,
        Organization organization,
        Project project,
        CancellationToken cancellationToken);

    Task<EventBatchWriteResult> WriteAsync(IReadOnlyCollection<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken);
}

public sealed class EventBatchWriter(
    IEventRepository eventRepository,
    IStackRepository stackRepository,
    IStackRouteResolver stackRouteResolver,
    ILockProvider lockProvider,
    IQueue<WorkItemData> workItemQueue,
    IEventIngestionIdStore eventIngestionIdStore,
    AppOptions options,
    TimeProvider timeProvider) : IEventBatchWriter
{
    public async Task<IReadOnlyList<EventIngestionIdentity>> PrepareAsync(
        IReadOnlyCollection<EventIngestionV3Event> events,
        string projectId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
            return [];

        var sources = events.ToArray();
        var idCandidates = new EventIngestionIdCandidate[sources.Length];
        DateTime createdUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        DateTimeOffset receiptDate = new(createdUtc);
        HashSet<string>? mappedClientIds = null;

        for (int i = 0; i < sources.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset eventDate = GetCanonicalEventDate(sources[i].Date, receiptDate);
            idCandidates[i] = new EventIngestionIdCandidate(
                sources[i].Id,
                new EventIngestionId(
                    GetDeterministicEventId(projectId, sources[i].Id, eventDate.UtcDateTime),
                    eventDate,
                    createdUtc));
            if (options.EventIngestionV3.EnableProcessingStatus
                || sources[i].Date is null
                || sources[i].Date > receiptDate)
                (mappedClientIds ??= new HashSet<string>(StringComparer.Ordinal)).Add(sources[i].Id);
        }

        IReadOnlyDictionary<string, EventIngestionId>? assignedIds = null;
        if (mappedClientIds is not null)
        {
            assignedIds = await eventIngestionIdStore.GetOrAddAsync(
                projectId,
                idCandidates.Where(candidate => mappedClientIds.Contains(candidate.ClientId)).ToArray(),
                options.EventIngestionV3.IdempotencyWindow,
                cancellationToken);
        }
        var eventIds = new string[sources.Length];
        var eventIdentities = new EventIngestionId[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            EventIngestionId identity = mappedClientIds?.Contains(sources[i].Id) is true
                ? assignedIds![sources[i].Id]
                : idCandidates[i].Identity;
            eventIds[i] = identity.EventId;
            eventIdentities[i] = identity;
        }

        string[] distinctIds = eventIds.Distinct(StringComparer.Ordinal).ToArray();
        var existingEvents = await eventRepository.GetByIdsAsync(distinctIds, o => o.Include(
            e => e.Id,
            e => e.StackId,
            e => e.Date,
            e => e.CreatedUtc));
        cancellationToken.ThrowIfCancellationRequested();
        var existingById = existingEvents.ToDictionary(e => e.Id, StringComparer.Ordinal);
        string[] persistedStackIds = existingEvents
            .Select(ev => ev.StackId)
            .Where(stackId => !String.IsNullOrEmpty(stackId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var persistedStacks = persistedStackIds.Length == 0
            ? []
            : await stackRepository.GetByIdsAsync(persistedStackIds, o => o.Include(stack => stack.Id, stack => stack.Status));
        var persistedStackStatuses = persistedStacks.ToDictionary(stack => stack.Id, stack => stack.Status, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var identities = new EventIngestionIdentity[sources.Length];
        DateTime recoveryCutoffUtc = timeProvider.GetUtcNow().UtcDateTime.Subtract(options.EventIngestionV3.IdempotencyWindow);
        for (int i = 0; i < sources.Length; i++)
        {
            bool isPersisted = existingById.TryGetValue(eventIds[i], out var existing);
            bool isDuplicate = isPersisted || !seen.Add(eventIds[i]);
            DateTime identityCreatedUtc = existing?.CreatedUtc ?? eventIdentities[i].CreatedUtc;
            DateTimeOffset eventDate = existing?.Date ?? eventIdentities[i].EventDate;
            identities[i] = new EventIngestionIdentity(
                sources[i].Id,
                eventIds[i],
                isDuplicate,
                isPersisted,
                existing?.StackId,
                existing is not null && persistedStackStatuses.TryGetValue(existing.StackId, out StackStatus stackStatus) ? stackStatus : null,
                !isPersisted || identityCreatedUtc > recoveryCutoffUtc,
                identityCreatedUtc,
                eventDate);
        }

        return identities;
    }

    public async Task ReconcileAsync(
        IReadOnlyCollection<EventIngestionReconciliation> reconciliations,
        Organization organization,
        Project project,
        CancellationToken cancellationToken)
    {
        if (reconciliations.Count == 0)
            return;

        var requestedStackIds = reconciliations
            .GroupBy(item => item.EventId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().StackId, StringComparer.Ordinal);
        var persistedEvents = await eventRepository.GetByIdsAsync(requestedStackIds.Keys.ToArray(), o => o.Include(
            ev => ev.Id,
            ev => ev.StackId,
            ev => ev.IsRegression,
            ev => ev.IngestionIsRegressionCandidate,
            ev => ev.IngestionRegressionFixedInVersion,
            ev => ev.IngestionRegressionDateFixed));
        string[] stackIds = persistedEvents
            .Where(ev => requestedStackIds.TryGetValue(ev.Id, out string? requestedStackId)
                && String.Equals(requestedStackId, ev.StackId, StringComparison.Ordinal))
            .Select(ev => ev.StackId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var stacks = stackIds.Length == 0
            ? []
            : await stackRepository.GetByIdsAsync(stackIds, o => o.Include(
                stack => stack.Id,
                stack => stack.Status,
                stack => stack.RegressionEventId));
        var stacksById = stacks.ToDictionary(stack => stack.Id, StringComparer.Ordinal);
        var activeEventIds = new List<string>(persistedEvents.Count);
        foreach (var ev in persistedEvents)
        {
            if (!requestedStackIds.TryGetValue(ev.Id, out string? requestedStackId)
                || !String.Equals(requestedStackId, ev.StackId, StringComparison.Ordinal)
                || !stacksById.TryGetValue(ev.StackId, out Stack? stack)
                || stack.Status == StackStatus.Discarded)
                continue;

            activeEventIds.Add(ev.Id);
            StackRoute? regressionRoute = null;
            if (ev.IngestionIsRegressionCandidate && ev.IngestionRegressionDateFixed != DateTime.MinValue)
            {
                regressionRoute = new StackRoute(
                    ev.StackId,
                    StackStatus.Fixed,
                    0,
                    ev.IngestionRegressionFixedInVersion,
                    ev.IngestionRegressionDateFixed,
                    RegressionEventId: stack.RegressionEventId);
            }
            else if (stack.RegressionEventId == ev.Id)
            {
                regressionRoute = new StackRoute(
                    ev.StackId,
                    stack.Status,
                    0,
                    RegressionEventId: stack.RegressionEventId);
            }

            if (regressionRoute is not null)
            {
                // Reconciliation deliberately loads only routing fields above. CompleteRegressionAsync
                // must reload the full document before SaveAsync so a duplicate retry cannot validate or
                // overwrite the event using that source-filtered projection.
                await CompleteRegressionAsync(ev.Id, regressionRoute, cancellationToken);
            }
        }

        await EnqueueSideEffectsAsync(
            activeEventIds.Distinct(StringComparer.Ordinal).ToArray(),
            organization.Id,
            project.Id);
    }

    public async Task<EventBatchWriteResult> WriteAsync(IReadOnlyCollection<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken)
    {
        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Batch Write");
        if (writes.Count == 0)
            return new EventBatchWriteResult(0, 0, []);

        var uniqueWrites = writes
            .GroupBy(write => write.Event.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        // PrepareAsync already checked these deterministic IDs. Avoid a second Elasticsearch
        // multi-get on every successful microbatch; create-only conflicts are rare and take the
        // reconciliation path below.
        var existing = new List<PersistentEvent>();
        var writesToAdd = uniqueWrites;
        using (AppDiagnostics.StartActivity("Ingestion V3 Stack Resolve Create"))
            await AssignStacksAsync(writesToAdd, organization, project, cancellationToken);
        var eventsToAdd = writesToAdd.Select(write => write.Event).ToList();
        foreach (var write in writesToAdd)
        {
            PersistentEvent ev = write.Event;
            if (write.IsRegressionCandidate && write.Route is { Status: StackStatus.Fixed, DateFixed: not null } route)
            {
                ev.IngestionIsRegressionCandidate = true;
                ev.IngestionRegressionFixedInVersion = route.FixedInVersion;
                ev.IngestionRegressionDateFixed = route.DateFixed.Value;
            }
        }
        EventUsageSettlement[] settlements = [];

        if (eventsToAdd.Count > 0)
        {
            try
            {
                await eventRepository.AddAsync(eventsToAdd);
                // The writer that receives a successful add owns billing for these documents.
                // Existing documents and ambiguous bulk outcomes never produce settlements,
                // so a retry can recover side effects without charging the event twice.
                settlements = GetPersistedSettlements(eventsToAdd);
            }
            catch (Exception ex)
            {
                AppDiagnostics.IngestionV3WriteReconciliations.Add(1);
                var reconciled = await eventRepository.GetByIdsAsync(eventsToAdd.Select(e => e.Id).ToArray(), o => o.Include(
                    e => e.Id,
                    e => e.StackId,
                    e => e.CreatedUtc));
                AppDiagnostics.IngestionV3AmbiguousSettlementsSkipped.Add(reconciled.Count);
                if (reconciled.Count != eventsToAdd.Count)
                {
                    // Elasticsearch did not tell us which documents this request created.
                    // Fail open for billing rather than risk charging a racing retry twice.
                    throw new EventBatchWriteException(ex, []);
                }

                // The bulk outcome is ambiguous. From this point on use only the durable
                // documents; never let replay-materialized payloads drive regression repair.
                existing.AddRange(reconciled.DistinctBy(ev => ev.Id, StringComparer.Ordinal));
                writesToAdd.Clear();
                eventsToAdd.Clear();
            }
        }

        try
        {
            if (existing.Count > 0)
            {
                await ReconcileAsync(
                    existing
                        .Where(ev => !String.IsNullOrEmpty(ev.StackId))
                        .Select(ev => new EventIngestionReconciliation(ev.Id, ev.StackId))
                        .ToArray(),
                    organization,
                    project,
                    cancellationToken);
            }

            foreach (var write in writesToAdd.Where(write => write.IsRegressionCandidate || write.Route?.RegressionEventId == write.Event.Id))
            {
                await CompleteRegressionAsync(
                    write.Event.Id,
                    write.Route,
                    cancellationToken,
                    write.Event);
            }

            await EnqueueSideEffectsAsync(eventsToAdd.Select(ev => ev.Id).ToArray(), organization.Id, project.Id);
        }
        catch (Exception ex) when (ex is not EventBatchWriteException)
        {
            throw new EventBatchWriteException(ex, settlements);
        }

        return new EventBatchWriteResult(eventsToAdd.Count, writes.Count - eventsToAdd.Count, settlements);
    }

    private static EventUsageSettlement[] GetPersistedSettlements(IReadOnlyCollection<PersistentEvent> events)
    {
        if (events.Count == 0)
            return [];

        return events
            .DistinctBy(ev => ev.Id, StringComparer.Ordinal)
            .Select(ev => new EventUsageSettlement(ev.Id, ev.CreatedUtc))
            .ToArray();
    }

    private async Task CompleteRegressionAsync(
        string eventId,
        StackRoute? route,
        CancellationToken cancellationToken,
        PersistentEvent? knownCompleteEvent = null)
    {
        if (route is null)
            return;

        bool isRegression = route.RegressionEventId == eventId;
        if (!isRegression && route.Status == StackStatus.Fixed)
        {
            isRegression = await stackRouteResolver.TryMarkRegressedAsync(route, eventId, cancellationToken);
            if (!isRegression)
            {
                var currentStack = await stackRepository.GetByIdAsync(route.StackId);
                isRegression = currentStack?.RegressionEventId == eventId;
            }
        }

        if (!isRegression)
            return;

        var ev = knownCompleteEvent ?? await eventRepository.GetByIdAsync(eventId);
        if (ev is null || ev.IsRegression)
            return;

        ev.IsRegression = true;
        await eventRepository.SaveAsync(ev, o => o.Notifications(false));
    }

    private async Task EnqueueSideEffectsAsync(string[] eventIds, string organizationId, string projectId)
    {
        if (eventIds.Length == 0)
            return;

        Array.Sort(eventIds, StringComparer.Ordinal);
        using (AppDiagnostics.StartActivity("Ingestion V3 Outbox Write"))
        using (AppDiagnostics.IngestionV3OutboxWriteTime.StartTimer())
            await workItemQueue.EnqueueAsync(new EventIngestionSideEffectsWorkItem
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                BatchId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join(':', eventIds)))).ToLowerInvariant(),
                EventIds = eventIds
            });
    }

    private Task AssignStacksAsync(List<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken)
    {
        foreach (var write in writes.Where(write => write.Route is not null))
        {
            write.Event.StackId = write.Route!.StackId;
            write.Event.IsFirstOccurrence = String.Equals(
                write.Event.Id,
                write.Route.IngestionFirstEventId,
                StringComparison.Ordinal);
        }

        var missingGroups = writes
            .Where(write => write.Route is null)
            .GroupBy(write => write.Fingerprint.SignatureHash)
            .ToArray();
        return Parallel.ForEachAsync(missingGroups, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.EventIngestionV3.MaximumStackCreationConcurrency
        }, async (group, token) =>
        {
            token.ThrowIfCancellationRequested();
            Stack? stack = null;
            bool isNew = false;
            bool acquired = await lockProvider.TryUsingAsync($"new-stack:{project.Id}:{group.Key}", async () =>
            {
                stack = await stackRepository.GetStackBySignatureHashAsync(project.Id, group.Key);
                if (stack is not null)
                    return;

                EventIngestionWrite first = group.First();
                EventIngestionWrite firstOccurrence = group
                    .OrderBy(write => write.Event.Date.UtcDateTime)
                    .ThenBy(write => write.Event.Id, StringComparer.Ordinal)
                    .First();
                stack = new Stack
                {
                    OrganizationId = organization.Id,
                    ProjectId = project.Id,
                    SignatureInfo = new SettingsDictionary(first.Fingerprint.SignatureData),
                    SignatureHash = group.Key,
                    DuplicateSignature = String.Concat(project.Id, ":", group.Key),
                    Title = GetStackTitle(first).Truncate(1000),
                    Tags = first.Event.Tags ?? [],
                    Type = first.Event.Type ?? Event.KnownTypes.Log,
                    TotalOccurrences = 0,
                    FirstOccurrence = group.Min(write => write.Event.Date.UtcDateTime),
                    LastOccurrence = group.Max(write => write.Event.Date.UtcDateTime),
                    IngestionFirstEventId = firstOccurrence.Event.Id,
                    CreatedUtc = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime
                };
                await stackRepository.AddAsync(stack, o => o.Cache());
                isNew = true;
            }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

            if (!acquired || stack is null)
                throw new InvalidOperationException($"Unable to resolve stack for signature '{group.Key}'.");

            foreach (var write in group)
            {
                write.Event.StackId = stack.Id;
                write.Event.IsFirstOccurrence = String.Equals(
                    write.Event.Id,
                    stack.IngestionFirstEventId,
                    StringComparison.Ordinal);
            }

            // AddAsync publishes an authoritative route through repository invalidation. A stack
            // found after our earlier route miss still needs to replace that negative entry.
            if (!isNew)
                await stackRouteResolver.UpdateAsync(project.Id, group.Key, StackRouteResolver.CreateRoute(stack));
        });
    }

    internal static string GetDeterministicEventId(string projectId, string clientId, DateTime eventDateUtc)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(String.Concat(projectId, ":", clientId)));
        Span<byte> objectId = stackalloc byte[12];
        long timestamp = new DateTimeOffset(DateTime.SpecifyKind(eventDateUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        BinaryPrimitives.WriteUInt32BigEndian(objectId, checked((uint)timestamp));
        hash.AsSpan(0, 8).CopyTo(objectId[4..]);
        return Convert.ToHexStringLower(objectId);
    }

    private static DateTimeOffset GetCanonicalEventDate(DateTimeOffset? requestedDate, DateTimeOffset receiptDate)
    {
        if (requestedDate is not { } date || date > receiptDate)
            return receiptDate;
        return date < DateTimeOffset.UnixEpoch ? DateTimeOffset.UnixEpoch : date;
    }

    private static string GetStackTitle(EventIngestionWrite write)
    {
        if (!String.IsNullOrWhiteSpace(write.Fingerprint.Title))
            return write.Fingerprint.Title;
        if (write.Fingerprint.SignatureData.TryGetValue("ExceptionType", out string? exceptionType))
            return String.IsNullOrWhiteSpace(write.Event.Message) ? exceptionType : String.Concat(exceptionType, ": ", write.Event.Message);

        if (!String.IsNullOrWhiteSpace(write.Event.Message))
            return write.Event.Message;
        if (!String.IsNullOrWhiteSpace(write.Event.Source))
            return write.Event.Source;
        return write.Event.Type ?? Event.KnownTypes.Log;
    }
}
