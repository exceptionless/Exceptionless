using System;
using Exceptionless.Core.Dependency;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.WebHook {
    public class WebHookDataPluginManager : PluginManagerBase<IWebHookDataPlugin> {
        public WebHookDataPluginManager(IDependencyResolver dependencyResolver = null) : base(dependencyResolver) {}

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public object CreateFromEvent(WebHookDataContext context) {
            foreach (var plugin in Plugins.Values) {
                try {
                    var data = plugin.CreateFromEvent(context);
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling create from event in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public object CreateFromStack(WebHookDataContext context) {
            foreach (var plugin in Plugins.Values) {
                try {
                    var data = plugin.CreateFromStack(context);
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling create from stack in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }
    }
}