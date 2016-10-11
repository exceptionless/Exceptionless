using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.WebHook {
    public abstract class WebHookDataPluginBase : IWebHookDataPlugin {
        public abstract Task<object> CreateFromEventAsync(WebHookDataContext ctx);

        public abstract Task<object> CreateFromStackAsync(WebHookDataContext ctx);
    }
}
