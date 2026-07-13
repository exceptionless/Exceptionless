using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Foundatio.Caching;
using Foundatio.Queues;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class EventPostService
{
    private static readonly TimeSpan _processingTrackingTtl = TimeSpan.FromDays(1);
    private readonly IQueue<EventPost> _queue;
    private readonly IFileStorage _storage;
    private readonly ICacheClient _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public EventPostService(IQueue<EventPost> queue, IFileStorage storage, ICacheClient cache,
        TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _queue = queue;
        _storage = storage;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<EventPostService>();
    }

    public async Task<bool> InitializeProcessingTrackingAsync(string correlationId, string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var status = new EventPostProcessingStatus(projectId, false, _timeProvider.GetUtcNow());
        try
        {
            bool statusAdded = await _cache.AddAsync(GetProcessingStatusCacheKey(correlationId), status, _processingTrackingTtl);
            bool pendingAdded = await _cache.AddAsync(GetProcessingPendingCacheKey(correlationId), 1L, _processingTrackingTtl);
            if (statusAdded && pendingAdded)
                return true;

            var existingStatus = await _cache.GetAsync<EventPostProcessingStatus>(GetProcessingStatusCacheKey(correlationId));
            var existingPending = await _cache.GetAsync<long>(GetProcessingPendingCacheKey(correlationId));
            return existingStatus is { HasValue: true, Value.ProjectId: var existingProjectId }
                && String.Equals(existingProjectId, projectId, StringComparison.Ordinal)
                && existingPending is { HasValue: true, Value: >= 0 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to initialize event post processing tracking {CorrelationId} for project {ProjectId}", correlationId, projectId);
        }

        return false;
    }

    public async Task<bool> AddPendingProcessingUnitsAsync(EventPost eventPost, int count)
    {
        if (String.IsNullOrEmpty(eventPost.ProcessingCorrelationId) || count <= 0)
            return true;

        try
        {
            long pending = await _cache.IncrementAsync(
                GetProcessingPendingCacheKey(eventPost.ProcessingCorrelationId),
                count,
                _processingTrackingTtl);

            // Increment creates a missing key at exactly count. A valid correlation always has
            // at least its parent unit, so fail closed instead of completing descendants early.
            return pending > count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to add {Count} pending units to event post processing tracking {CorrelationId}", count, eventPost.ProcessingCorrelationId);
            return false;
        }
    }

    public async Task MarkProcessingCompletedAsync(string queueEntryId, EventPost eventPost, bool completeTracking = true)
    {
        if (!completeTracking || String.IsNullOrEmpty(eventPost.ProcessingCorrelationId))
            return;

        try
        {
            var pendingState = await _cache.GetAsync<long>(GetProcessingPendingCacheKey(eventPost.ProcessingCorrelationId));
            if (!pendingState.HasValue || pendingState.Value <= 0)
                return;

            string completedUnitKey = GetProcessingCompletedUnitCacheKey(eventPost.ProcessingCorrelationId, queueEntryId);
            if (!await _cache.AddAsync(completedUnitKey, true, _processingTrackingTtl))
                return;

            long pending = await _cache.DecrementAsync(
                GetProcessingPendingCacheKey(eventPost.ProcessingCorrelationId),
                1,
                _processingTrackingTtl);
            if (pending > 0)
                return;

            if (pending < 0)
            {
                _logger.LogWarning("Event post processing tracking {CorrelationId} has an invalid pending count of {PendingCount}", eventPost.ProcessingCorrelationId, pending);
                return;
            }

            var status = new EventPostProcessingStatus(eventPost.ProjectId, true, _timeProvider.GetUtcNow());
            await _cache.SetAsync(GetProcessingStatusCacheKey(eventPost.ProcessingCorrelationId), status, _processingTrackingTtl);
        }
        catch (Exception ex)
        {
            // Observability must never make an otherwise successful event post retry. A partial
            // marker update remains non-terminal, causing the benchmark to fail closed on timeout.
            _logger.LogWarning(ex, "Unable to complete event post processing unit {QueueEntryId} for tracking {CorrelationId}", queueEntryId, eventPost.ProcessingCorrelationId);
        }
    }

    public async Task<IReadOnlyDictionary<string, EventPostProcessingStatus>> GetProcessingStatusesAsync(IEnumerable<string> queueEntryIds)
    {
        string[] ids = queueEntryIds.Distinct(StringComparer.Ordinal).ToArray();
        var keys = ids.ToDictionary(id => id, GetProcessingStatusCacheKey, StringComparer.Ordinal);
        var cached = await _cache.GetAllAsync<EventPostProcessingStatus>(keys.Values);
        var statuses = new Dictionary<string, EventPostProcessingStatus>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (cached.TryGetValue(keys[id], out var value) && value.HasValue)
                statuses[id] = value.Value;
        }

        return statuses;
    }

    public async Task<string?> EnqueueAsync(EventPost data, Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await SaveAndEnqueueAsync(data, stream, cancellationToken);
        return result.QueueEntryId;
    }

    public async Task<EventPostEnqueueResult> SaveAndEnqueueAsync(EventPost data, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (data.ShouldArchive)
        {
            data.FilePath = GetArchivePath(_timeProvider.GetUtcNow().UtcDateTime, data.ProjectId, $"{Guid.NewGuid():N}.json");
        }
        else
        {
            string fileId = Guid.NewGuid().ToString("N");
            data.FilePath = Path.Combine("q", fileId.Substring(0, 3), $"{fileId}.json");
        }

        var saveTask = data.ShouldArchive ? _storage.SaveObjectAsync(data.FilePath, (EventPostInfo)data, cancellationToken) : Task.FromResult(true);
        var savePayloadTask = _storage.SaveFileAsync(Path.ChangeExtension(data.FilePath, ".payload"), stream, cancellationToken);

        bool infoSaved = await saveTask;
        bool payloadSaved = await savePayloadTask;

        if (stream is IEventPostBodyReadState { RejectedStatusCode: { } statusCode } rejectedBody)
        {
            await DeleteSavedEventPostFilesAsync(data);
            return EventPostEnqueueResult.Rejected(statusCode, rejectedBody.RejectionReason);
        }

        if (!infoSaved)
        {
            using (_logger.BeginScope(new ExceptionlessState().Organization(data.OrganizationId).Property(nameof(EventPostInfo), data)))
                _logger.LogError("Unable to save event post info");

            return EventPostEnqueueResult.Failed;
        }

        if (!payloadSaved)
        {
            using (_logger.BeginScope(new ExceptionlessState().Organization(data.OrganizationId).Property(nameof(EventPostInfo), data)))
                _logger.LogError("Unable to save event post payload");

            return EventPostEnqueueResult.Failed;
        }

        string? queueEntryId = await _queue.EnqueueAsync(data);
        return !String.IsNullOrEmpty(queueEntryId) ? EventPostEnqueueResult.Queued(queueEntryId) : EventPostEnqueueResult.Failed;
    }

    public async Task<byte[]?> GetEventPostPayloadAsync(string path)
    {
        if (String.IsNullOrEmpty(path))
            return null;

        byte[]? data;
        try
        {
            data = await _storage.GetFileContentsRawAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving event post payload: {Path}", path);
            return null;
        }

        return data;
    }

    public async Task<bool> CompleteEventPostAsync(string path, string projectId, DateTime created, bool shouldArchive = true)
    {
        if (String.IsNullOrEmpty(path))
            return false;

        // don't move files that are already in the archive
        if (path.StartsWith("archive"))
            return true;

        try
        {
            if (shouldArchive)
            {
                string archivePath = GetArchivePath(created, projectId, Path.GetFileName(path));
                var renameTask = _storage.RenameFileAsync(path, archivePath);
                var renamePayLoadTask = _storage.RenameFileAsync(Path.ChangeExtension(path, ".payload"), Path.ChangeExtension(archivePath, ".payload"));
                return await renameTask && await renamePayLoadTask;
            }

            return await _storage.DeleteFileAsync(Path.ChangeExtension(path, ".payload"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving event post data {Path}", path);
            return false;
        }
    }

    private static string GetArchivePath(DateTime createdUtc, string projectId, string fileName)
    {
        return Path.Combine("archive", createdUtc.ToString("yy"), createdUtc.ToString("MM"), createdUtc.ToString("dd"), createdUtc.ToString("HH"), createdUtc.ToString("mm"), projectId, fileName);
    }

    private static string GetProcessingStatusCacheKey(string correlationId)
    {
        return $"event-post-status:{correlationId.ToSHA256()}";
    }

    private static string GetProcessingPendingCacheKey(string correlationId) =>
        String.Concat(GetProcessingStatusCacheKey(correlationId), ":pending");

    private static string GetProcessingCompletedUnitCacheKey(string correlationId, string queueEntryId) =>
        String.Concat(GetProcessingStatusCacheKey(correlationId), ":completed:", queueEntryId.ToSHA256());

    private async Task DeleteSavedEventPostFilesAsync(EventPost data)
    {
        if (String.IsNullOrEmpty(data.FilePath))
            return;

        try
        {
            var tasks = new List<Task<bool>>
            {
                _storage.DeleteFileAsync(Path.ChangeExtension(data.FilePath, ".payload"))
            };

            if (data.ShouldArchive)
                tasks.Add(_storage.DeleteFileAsync(data.FilePath));

            await Task.WhenAll(tasks);
        }
        catch (StorageException ex)
        {
            using (_logger.BeginScope(new ExceptionlessState().Organization(data.OrganizationId).Property(nameof(EventPostInfo), data)))
                _logger.LogWarning(ex, "Unable to delete rejected event post payload");
        }
    }
}
