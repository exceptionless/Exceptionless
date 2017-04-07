using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.WebHook {
    public interface IWebHookDataPlugin : IPlugin {
        Task<object> CreateFromEventAsync(WebHookDataContext ctx);
        Task<object> CreateFromStackAsync(WebHookDataContext ctx);
    }
}
