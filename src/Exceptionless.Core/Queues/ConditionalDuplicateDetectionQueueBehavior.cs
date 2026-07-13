using Foundatio.Caching;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Queues;

/// <summary>
/// Preserves Foundatio's dequeue-scoped duplicate detection for legacy messages while allowing
/// durable messages to be claimed exclusively by <see cref="DurableDuplicateDetectionQueueBehavior{T}"/>.
/// </summary>
public class ConditionalDuplicateDetectionQueueBehavior<T>(
    ICacheClient cache,
    ILoggerFactory loggerFactory,
    TimeSpan detectionWindow) : QueueBehaviorBase<T> where T : class
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<T>();

    protected override async Task OnEnqueuing(object sender, EnqueuingEventArgs<T> args)
    {
        string? identifier = GetIdentifier(args.Data);
        if (String.IsNullOrEmpty(identifier) || await cache.AddAsync(identifier, true, detectionWindow))
            return;

        _logger.LogInformation("Discarding queue entry due to duplicate {UniqueIdentifier}", identifier);
        args.Cancel = true;
    }

    protected override async Task OnDequeued(object sender, DequeuedEventArgs<T> args)
    {
        string? identifier = GetIdentifier(args.Entry.Value);
        if (!String.IsNullOrEmpty(identifier))
            await cache.RemoveAsync(identifier);
    }

    private static string? GetIdentifier(T data)
    {
        if (data is IHaveDurableUniqueIdentifier { UseDurableDeduplication: true })
            return null;

        return (data as IHaveUniqueIdentifier)?.UniqueIdentifier;
    }
}
