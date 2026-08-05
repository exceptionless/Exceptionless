using System.Net;
using System.Net.Sockets;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.SourceMaps;

public sealed class SourceMapRequestThrottle
{
    private readonly ICacheClient _cache;
    private readonly SourceMapOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SourceMapRequestThrottle> _logger;

    public SourceMapRequestThrottle(ICacheClient cache, AppOptions options, TimeProvider timeProvider, ILogger<SourceMapRequestThrottle> logger)
    {
        _cache = cache;
        _options = options.SourceMapOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    internal async Task<bool> TryReserveDiscoveryAsync(SourceMapRequest request)
    {
        int clientKeyLimit = request.IsFreePlan
            ? _options.MaximumAutoDiscoveriesPerFreeClientKey
            : _options.MaximumAutoDiscoveriesPerClientKey;
        if (!String.IsNullOrEmpty(request.ClientKeyHash)
            && !await TryReserveBucketAsync("client-key", $"{request.ProjectId}:{request.ClientKeyHash}", clientKeyLimit))
        {
            return false;
        }

        int projectLimit = request.IsFreePlan
            ? _options.MaximumAutoDiscoveriesPerFreeProject
            : _options.MaximumAutoDiscoveriesPerProject;
        if (!await TryReserveBucketAsync("project", request.ProjectId, projectLimit))
            return false;

        int organizationLimit = request.IsFreePlan
            ? _options.MaximumAutoDiscoveriesPerFreeOrganization
            : _options.MaximumAutoDiscoveriesPerOrganization;
        return await TryReserveBucketAsync("organization", request.OrganizationId, organizationLimit);
    }

    internal async Task<bool> TryReserveOutboundRequestAsync(Uri uri, bool isRefresh = false)
    {
        string destination = uri.IdnHost.ToLowerInvariant().ToSHA256();
        string scopePrefix = isRefresh ? "refresh-" : String.Empty;
        int destinationLimit = isRefresh
            ? _options.MaximumAutoRefreshRequestsPerDestination
            : _options.MaximumAutoDownloadRequestsPerDestination;
        if (!await TryReserveBucketAsync(scopePrefix + "destination", destination, destinationLimit))
            return false;

        int globalLimit = isRefresh
            ? _options.MaximumAutoRefreshRequestsGlobally
            : _options.MaximumAutoDownloadRequestsGlobally;
        return await TryReserveBucketAsync(scopePrefix + "global", "all", globalLimit);
    }

    internal async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? lastException = null;
        bool addressThrottled = false;
        foreach (var address in addresses)
        {
            if (!OAuthClientMetadataService.IsPublicAddress(address))
                continue;

            string addressHash = address.ToString().ToSHA256();
            if (!await TryReserveBucketAsync("ip-address", addressHash, _options.MaximumAutoDownloadConnectionsPerIpAddress))
            {
                addressThrottled = true;
                continue;
            }

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                lastException = ex;
                socket.Dispose();
            }
        }

        if (addressThrottled)
            throw new SourceMapRequestThrottledException();

        throw new HttpRequestException($"Host '{context.DnsEndPoint.Host}' did not resolve to a reachable public address.", lastException);
    }

    private async Task<bool> TryReserveBucketAsync(string scope, string identifier, int limit)
    {
        var window = GetCurrentWindow();
        string counterKey = $"source-maps:rate:{scope}:{identifier}:{window.Id}";
        string blockedKey = counterKey + ":blocked";
        if ((await _cache.GetAsync<bool>(blockedKey)).HasValue)
            return false;

        long count = await _cache.IncrementAsync(counterKey, 1, window.CounterLifetime);
        if (count <= limit)
            return true;

        await _cache.SetAsync(blockedKey, true, window.Remaining);
        if (count == limit + 1)
            _logger.LogWarning("Source map automatic download {RateLimitScope} rate limit reached.", scope);

        return false;
    }

    private RateLimitWindow GetCurrentWindow()
    {
        long periodSeconds = Math.Max(1, (long)_options.AutoDownloadRateLimitPeriod.TotalSeconds);
        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        long id = now / periodSeconds;
        long remainingSeconds = Math.Max(1, ((id + 1) * periodSeconds) - now);
        return new RateLimitWindow(id, TimeSpan.FromSeconds(remainingSeconds), TimeSpan.FromSeconds(periodSeconds * 2));
    }

    private sealed record RateLimitWindow(long Id, TimeSpan Remaining, TimeSpan CounterLifetime);
}

internal sealed record SourceMapRequest(string OrganizationId, string ProjectId, string? ClientKeyHash, bool IsFreePlan);

internal sealed class SourceMapRequestThrottledException : Exception;
