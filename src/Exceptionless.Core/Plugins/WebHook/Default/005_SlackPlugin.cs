using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.Formatting;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

[Priority(5)]
public sealed class SlackPlugin : WebHookDataPluginBase
{
    private readonly FormattingPluginManager _pluginManager;
    private readonly ITextSerializer _serializer;

    public SlackPlugin(FormattingPluginManager pluginManager, ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _pluginManager = pluginManager;
        _serializer = serializer;
    }

    public override Task<object?> CreateFromEventAsync(WebHookDataContext ctx)
    {
        if (String.IsNullOrEmpty(ctx.WebHook.Url) || !ctx.WebHook.Url.EndsWith("/slack"))
            return Task.FromResult<object?>(null);

        var error = ctx.Event?.GetError(_serializer);
        if (error is null)
        {
            ctx.IsCancelled = true;
            return Task.FromResult<object?>(null);
        }

        var message = _pluginManager.GetSlackEventNotificationMessage(ctx.Event!, ctx.Project, ctx.Event!.IsCritical(), ctx.IsNew, ctx.IsRegression);
        ctx.IsCancelled = message is null;
        return Task.FromResult<object?>(message);
    }

    public override Task<object?> CreateFromStackAsync(WebHookDataContext ctx)
    {
        if (!String.IsNullOrEmpty(ctx.WebHook.Url) && ctx.WebHook.Url.EndsWith("/slack"))
            ctx.IsCancelled = true;

        return Task.FromResult<object?>(null);
    }
}
