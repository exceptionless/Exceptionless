using System.Threading.RateLimiting;
using Exceptionless.Core;

namespace Exceptionless.Web.Utility;

internal sealed class EventIngestionV3ConcurrencyLimiter : IAsyncDisposable
{
    private const string GlobalPartitionKey = "event-ingestion-v3";
    private readonly ConcurrencyLimiter _globalActiveStreamLimiter;
    private readonly PartitionedRateLimiter<string> _organizationActiveStreamLimiter;
    private readonly PartitionedRateLimiter<string> _processingLimiter;

    public EventIngestionV3ConcurrencyLimiter(AppOptions options)
    {
        EventIngestionV3Options ingestionOptions = options.EventIngestionV3;
        _globalActiveStreamLimiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = ingestionOptions.MaximumActiveStreams,
            QueueLimit = ingestionOptions.ActiveStreamQueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
        _organizationActiveStreamLimiter = CreateOrganizationLimiter(
            ingestionOptions.MaximumActiveStreamsPerOrganization,
            ingestionOptions.ActiveStreamQueueLimitPerOrganization);
        _processingLimiter = CreateChainedLimiter(
            ingestionOptions.MaximumConcurrentRequests,
            ingestionOptions.ConcurrencyQueueLimit,
            ingestionOptions.MaximumConcurrentRequestsPerOrganization,
            ingestionOptions.ConcurrencyQueueLimitPerOrganization);
    }

    public ValueTask<RateLimitLease> AcquireGlobalActiveStreamAsync(CancellationToken cancellationToken) =>
        _globalActiveStreamLimiter.AcquireAsync(cancellationToken: cancellationToken);

    public ValueTask<RateLimitLease> AcquireOrganizationActiveStreamAsync(string organizationId, CancellationToken cancellationToken) =>
        _organizationActiveStreamLimiter.AcquireAsync(organizationId, cancellationToken: cancellationToken);

    public ValueTask<RateLimitLease> AcquireProcessingAsync(string organizationId, CancellationToken cancellationToken) =>
        _processingLimiter.AcquireAsync(organizationId, cancellationToken: cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _globalActiveStreamLimiter.DisposeAsync();
        await _organizationActiveStreamLimiter.DisposeAsync();
        await _processingLimiter.DisposeAsync();
    }

    private static PartitionedRateLimiter<string> CreateOrganizationLimiter(
        int organizationPermitLimit,
        int organizationQueueLimit) =>
        PartitionedRateLimiter.Create<string, string>(organizationId =>
            RateLimitPartition.GetConcurrencyLimiter(
                organizationId,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = organizationPermitLimit,
                    QueueLimit = organizationQueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

    private static PartitionedRateLimiter<string> CreateChainedLimiter(
        int globalPermitLimit,
        int globalQueueLimit,
        int organizationPermitLimit,
        int organizationQueueLimit)
    {
        var organizationLimiter = CreateOrganizationLimiter(
            organizationPermitLimit,
            organizationQueueLimit);
        var globalLimiter = PartitionedRateLimiter.Create<string, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter(
                GlobalPartitionKey,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = globalPermitLimit,
                    QueueLimit = globalQueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

        // Wait at the organization boundary before consuming scarce global capacity.
        return PartitionedRateLimiter.CreateChained(organizationLimiter, globalLimiter);
    }
}
