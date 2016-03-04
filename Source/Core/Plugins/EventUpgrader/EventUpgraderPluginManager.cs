using System;
using System.Linq;
using Exceptionless.Core.Dependency;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public class EventUpgraderPluginManager : PluginManagerBase<IEventUpgraderPlugin> {
        public EventUpgraderPluginManager(IDependencyResolver dependencyResolver = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, loggerFactory) { }

        /// <summary>
        /// Runs all of the event upgrade plugins upgrade method.
        /// </summary>
        public void Upgrade(EventUpgraderContext context) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    plugin.Upgrade(context);
                } catch (Exception ex) {
                    _logger.Error().Exception(ex).Message("Error calling upgrade in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Property("Context", context).Write();
                    throw;
                }
            }
        }
    }
}
