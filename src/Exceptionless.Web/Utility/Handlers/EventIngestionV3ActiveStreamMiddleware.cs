using System.Threading.RateLimiting;
using Exceptionless.Core;
using Exceptionless.Web.Endpoints;

namespace Exceptionless.Web.Utility.Handlers;

internal sealed class EventIngestionV3ActiveStreamMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        EventIngestionV3ConcurrencyLimiter concurrencyLimiter,
        AppOptions options)
    {
        if (!options.EventIngestionV3.Enabled
            || options.EventSubmissionDisabled
            || context.GetEndpoint()?.Metadata.GetMetadata<EventIngestionV3EndpointMetadata>() is null)
        {
            await next(context);
            return;
        }

        // This global permit is intentionally acquired before the request-body middleware raises
        // Kestrel's limit. The endpoint acquires the routed organization's permit after project
        // authorization, without consuming a second global permit.
        using RateLimitLease lease = await concurrencyLimiter.AcquireGlobalActiveStreamAsync(context.RequestAborted);
        if (!lease.IsAcquired)
        {
            await Microsoft.AspNetCore.Http.Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Event ingestion stream capacity is busy.")
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
