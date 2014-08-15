using System;

namespace Exceptionless.Core.Plugins.WebHook {
    public abstract class WebHookDataPluginBase : IWebHookDataPlugin {
        public abstract object CreateFromEvent(WebHookDataContext ctx);

        public abstract object CreateFromStack(WebHookDataContext ctx);
    }
}
