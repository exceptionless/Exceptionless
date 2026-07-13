using Exceptionless.Core;
using Exceptionless.Web.Utility;
using Microsoft.AspNetCore.Http.Features;

namespace Exceptionless.Web.Utility.Handlers;

internal sealed class EventIngestionV3RequestBodyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppOptions options)
    {
        var ingestionOptions = options.EventIngestionV3;
        if (context.Request.ContentLength > ingestionOptions.MaximumCompressedBodySize)
        {
            await Microsoft.AspNetCore.Http.Results.Problem(
                statusCode: StatusCodes.Status413RequestEntityTooLarge,
                title: "The compressed request body is too large.").ExecuteAsync(context);
            return;
        }

        // The framework uses one feature value for both Kestrel's raw-body limit and
        // its decompression wrapper, so it cannot express our two independent limits.
        // Raise the shared ceiling only as far as the larger V3 limit; the wrappers
        // enforce the exact compressed and decompressed limits when the endpoint reads.
        // Keeping a finite transport ceiling also bounds Kestrel's unread-body drain
        // when an admitted request is rejected before its body is consumed.
        IHttpMaxRequestBodySizeFeature? requestSizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSizeFeature is { IsReadOnly: false })
        {
            requestSizeFeature.MaxRequestBodySize = Math.Max(
                ingestionOptions.MaximumCompressedBodySize,
                ingestionOptions.MaximumDecompressedBodySize);
        }

        var compressedBody = new EventPostRequestBodyStream(
            context.Request.Body,
            ingestionOptions.MaximumCompressedBodySize,
            "The compressed request body is too large.");
        context.Features.Set(new EventIngestionV3RequestBodyState(compressedBody));
        context.Request.Body = compressedBody;

        await next(context);
    }
}

internal sealed record EventIngestionV3RequestBodyState(EventPostRequestBodyStream CompressedBody);
