using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.WebHook {
    public interface IWebHookDataPlugin {
        Task<object> CreateFromEventAsync(WebHookDataContext ctx);
        Task<object> CreateFromStackAsync(WebHookDataContext ctx);
    }
}
