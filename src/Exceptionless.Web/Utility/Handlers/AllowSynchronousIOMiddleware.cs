using Microsoft.AspNetCore.Http.Features;

namespace Exceptionless.Web.Utility.Handlers;

public class AllowSynchronousIOMiddleware
{
    private readonly RequestDelegate _next;

    public AllowSynchronousIOMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
        if (syncIOFeature is not null)
            syncIOFeature.AllowSynchronousIO = true;

        return _next(context);
    }
}
