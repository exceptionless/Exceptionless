using Foundatio.Caching;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Queues;

public interface IHaveDurableUniqueIdentifier : IHaveUniqueIdentifier
{
    bool UseDurableDeduplication { get; }
}

/// <summary>
/// Keeps successful queue deduplication markers after dequeue. A short pending lease protects
/// against a producer dying after claiming an identifier but before the queue accepts the item.
/// </summary>
public class DurableDuplicateDetectionQueueBehavior<T>(
    ICacheClient cache,
    ILoggerFactory loggerFactory,
    TimeSpan detectionWindow) : QueueBehaviorBase<T> where T : class
{
    private static readonly TimeSpan PendingWindow = TimeSpan.FromMinutes(1);
    private readonly ILogger _logger = loggerFactory.CreateLogger<DurableDuplicateDetectionQueueBehavior<T>>();

    protected override async Task OnEnqueuing(object sender, EnqueuingEventArgs<T> args)
    {
        string? identifier = GetIdentifier(args.Data);
        if (String.IsNullOrEmpty(identifier))
        {
            return;
        }

        string key = GetCacheKey(identifier);
        if (await cache.AddAsync(key, "pending", PendingWindow))
        {
            return;
        }

        var existing = await cache.GetAsync<string>(key);
        if (existing.HasValue && String.Equals(existing.Value, "completed", StringComparison.Ordinal))
        {
            _logger.LogDebug("Discarding durable duplicate queue entry {UniqueIdentifier}", identifier);
            args.Cancel = true;
            return;
        }

        // Do not turn an abandoned producer lease into a permanent false success. The caller can
        // retry after the short lease expires, preserving at-least-once delivery.
        throw new InvalidOperationException($"Queue identifier '{identifier}' is currently being enqueued.");
    }

    protected override async Task OnEnqueued(object sender, EnqueuedEventArgs<T> args)
    {
        string? identifier = GetIdentifier(args.Entry.Value);
        if (String.IsNullOrEmpty(identifier))
        {
            return;
        }

        await cache.SetAsync(GetCacheKey(identifier), "completed", detectionWindow);
    }

    internal static string GetCacheKey(string identifier) =>
        String.Concat("queue:durable-deduplication:", typeof(T).FullName, ":", identifier);

    private static string? GetIdentifier(T data) => data is IHaveDurableUniqueIdentifier { UseDurableDeduplication: true } durable
        ? durable.UniqueIdentifier
        : null;
}
