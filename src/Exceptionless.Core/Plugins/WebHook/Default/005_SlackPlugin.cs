using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.Formatting;

namespace Exceptionless.Core.Plugins.WebHook;

[Priority(5)]
public sealed class SlackPlugin : WebHookDataPluginBase {
    private readonly FormattingPluginManager _pluginManager;

    public SlackPlugin(FormattingPluginManager pluginManager, AppOptions options) : base(options) {
        _pluginManager = pluginManager;
    }

    public override Task<object> CreateFromEventAsync(WebHookDataContext ctx) {
        if (String.IsNullOrEmpty(ctx.WebHook.Url) || !ctx.WebHook.Url.EndsWith("/slack"))
            return Task.FromResult<object>(null);

        var error = ctx.Event.GetError();
        if (error is null)
            return Task.FromResult<object>(null);

        var message = _pluginManager.GetSlackEventNotificationMessage(ctx.Event, ctx.Project, ctx.Event.IsCritical(), ctx.IsNew, ctx.IsRegression);
        return Task.FromResult<object>(message);
    }

    public override Task<object> CreateFromStackAsync(WebHookDataContext ctx) {
        if (!String.IsNullOrEmpty(ctx.WebHook.Url) && ctx.WebHook.Url.EndsWith("/slack"))
            ctx.IsCancelled = true;

        return Task.FromResult<object>(null);
    }
}
