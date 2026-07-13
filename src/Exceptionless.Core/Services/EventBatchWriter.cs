using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Queues;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services;

public interface IEventBatchWriter
{
    Task<EventBatchWriteResult> WriteAsync(IReadOnlyCollection<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken);
}

public sealed class EventBatchWriter(
    IEventRepository eventRepository,
    IStackRepository stackRepository,
    IStackRouteResolver stackRouteResolver,
    ILockProvider lockProvider,
    IQueue<WorkItemData> workItemQueue,
    ICacheClient cache,
    AppOptions options,
    TimeProvider timeProvider) : IEventBatchWriter
{
    public async Task<EventBatchWriteResult> WriteAsync(IReadOnlyCollection<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken)
    {
        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Batch Write");
        if (writes.Count == 0)
            return new EventBatchWriteResult(0, 0);

        var mutableWrites = writes.ToList();
        await AssignEventIdsAsync(mutableWrites, project.Id);

        var uniqueWrites = mutableWrites
            .GroupBy(write => write.Event.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        string[] ids = uniqueWrites.Select(write => write.Event.Id).ToArray();
        var existing = await eventRepository.GetByIdsAsync(ids, o => o.Include(e => e.Id));
        var existingIds = existing.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var writesToAdd = uniqueWrites.Where(write => !existingIds.Contains(write.Event.Id)).ToList();
        using (AppDiagnostics.StartActivity("Ingestion V3 Stack Resolve Create"))
            await AssignStacksAsync(writesToAdd, organization, project, cancellationToken);
        var eventsToAdd = writesToAdd.Select(write => write.Event).ToList();

        if (eventsToAdd.Count > 0)
        {
            try
            {
                await eventRepository.AddAsync(eventsToAdd);
            }
            catch
            {
                AppDiagnostics.IngestionV3WriteReconciliations.Add(1);
                await eventRepository.GetByIdsAsync(eventsToAdd.Select(e => e.Id).ToArray(), o => o.Include(e => e.Id));
                throw;
            }
        }

        Array.Sort(ids, StringComparer.Ordinal);
        using (AppDiagnostics.StartActivity("Ingestion V3 Outbox Write"))
        using (AppDiagnostics.IngestionV3OutboxWriteTime.StartTimer())
            await workItemQueue.EnqueueAsync(new EventIngestionSideEffectsWorkItem
            {
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                BatchId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join(':', ids)))).ToLowerInvariant(),
                EventIds = ids
            });

        return new EventBatchWriteResult(eventsToAdd.Count, writes.Count - eventsToAdd.Count);
    }

    private Task AssignStacksAsync(List<EventIngestionWrite> writes, Organization organization, Project project, CancellationToken cancellationToken)
    {
        foreach (var write in writes.Where(write => write.Route is not null))
            write.Event.StackId = write.Route!.StackId;

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
                    CreatedUtc = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime
                };
                await stackRepository.AddAsync(stack, o => o.Cache());
                isNew = true;
            }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

            if (!acquired || stack is null)
                throw new InvalidOperationException($"Unable to resolve stack for signature '{group.Key}'.");

            bool firstOccurrence = true;
            foreach (var write in group)
            {
                write.Event.StackId = stack.Id;
                write.Event.IsFirstOccurrence = isNew && firstOccurrence;
                firstOccurrence = false;
            }

            await stackRouteResolver.UpdateAsync(project.Id, group.Key, new StackRoute(stack.Id, stack.Status));
        });
    }

    private async Task AssignEventIdsAsync(List<EventIngestionWrite> writes, string projectId)
    {
        string[] keys = writes.Select(write => GetIdempotencyKey(projectId, write.ClientId)).ToArray();
        var cached = await cache.GetAllAsync<string>(keys);

        for (int i = 0; i < writes.Count; i++)
        {
            if (cached.TryGetValue(keys[i], out var cacheValue) && cacheValue.HasValue)
            {
                writes[i].Event.Id = cacheValue.Value;
                continue;
            }

            string eventId = GetDeterministicEventId(projectId, writes[i].ClientId, writes[i].Event.Date.UtcDateTime);
            if (!await cache.AddAsync(keys[i], eventId, options.EventIngestionV3.IdempotencyWindow))
            {
                var winner = await cache.GetAsync<string>(keys[i]);
                if (winner.HasValue)
                    eventId = winner.Value;
            }

            writes[i].Event.Id = eventId;
        }
    }

    internal static string GetDeterministicEventId(string projectId, string clientId, DateTime eventDateUtc)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(String.Concat(projectId, ":", clientId)));
        Span<byte> objectId = stackalloc byte[12];
        long timestamp = (long)(eventDateUtc.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
        BinaryPrimitives.WriteInt32BigEndian(objectId, checked((int)timestamp));
        hash.AsSpan(0, 8).CopyTo(objectId[4..]);
        return Convert.ToHexString(objectId).ToLowerInvariant();
    }

    private static string GetIdempotencyKey(string projectId, string clientId) => String.Concat("ingest-v3:id:", projectId, ":", clientId);

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
