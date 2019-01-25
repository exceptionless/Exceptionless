using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Plugins.WebHook {
    public abstract class WebHookDataPluginBase : PluginBase, IWebHookDataPlugin {
        protected WebHookDataPluginBase(IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) { }

        public abstract Task<object> CreateFromEventAsync(WebHookDataContext ctx);

        public abstract Task<object> CreateFromStackAsync(WebHookDataContext ctx);

    }
}
