using System;

namespace Exceptionless.Core.Plugins.WebHook {
    public interface IWebHookDataPlugin {
        object CreateFromEvent(WebHookDataContext ctx);
        object CreateFromStack(WebHookDataContext ctx);
    }
}
