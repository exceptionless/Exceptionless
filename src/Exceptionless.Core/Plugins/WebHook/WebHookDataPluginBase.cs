﻿using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

public abstract class WebHookDataPluginBase : PluginBase, IWebHookDataPlugin
{
    protected WebHookDataPluginBase(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public abstract Task<object?> CreateFromEventAsync(WebHookDataContext ctx);

    public abstract Task<object?> CreateFromStackAsync(WebHookDataContext ctx);

}
