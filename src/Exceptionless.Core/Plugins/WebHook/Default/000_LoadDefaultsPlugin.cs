using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

[Priority(0)]
public sealed class LoadDefaultsPlugin : WebHookDataPluginBase
{
    public LoadDefaultsPlugin(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
    }

    public override Task<object?> CreateFromEventAsync(WebHookDataContext ctx)
    {
        if (ctx.Organization is null)
            throw new ArgumentException("Organization not found.");
        if (ctx.Project is null)
            throw new ArgumentException("Project not found.");
        if (ctx.Stack is null)
            throw new ArgumentException("Stack not found.");
        if (ctx.Event is null)
            throw new ArgumentException("Event cannot be null.");

        return Task.FromResult<object?>(null);
    }

    public override Task<object?> CreateFromStackAsync(WebHookDataContext ctx)
    {
        if (ctx.Organization is null)
            throw new ArgumentException("Organization not found.");
        if (ctx.Project is null)
            throw new ArgumentException("Project not found.");
        if (ctx.Stack is null)
            throw new ArgumentException("Stack not found.");

        return Task.FromResult<object?>(null);
    }
}
