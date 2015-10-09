using System;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins.WebHook {
    public class WebHookDataPluginManager : PluginManagerBase<IWebHookDataPlugin> {
        public WebHookDataPluginManager(IDependencyResolver dependencyResolver = null) : base(dependencyResolver) {}

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public async Task<object> CreateFromEventAsync(WebHookDataContext context) {
            foreach (var plugin in Plugins.Values) {
                try {
                    var data = await plugin.CreateFromEventAsync(context).AnyContext();
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    Logger.Error().Exception(ex).Message("Error calling create from event in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Property("Event", context.Event).Write();
                }
            }

            return null;
        }

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public async Task<object> CreateFromStackAsync(WebHookDataContext context) {
            foreach (var plugin in Plugins.Values) {
                try {
                    var data = await plugin.CreateFromStackAsync(context).AnyContext();
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    Logger.Error().Exception(ex).Message("Error calling create from stack in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Property("Stack", context.Stack).Write();
                }
            }

            return null;
        }
    }
}