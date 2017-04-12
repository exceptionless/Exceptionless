using System;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public interface IEventUpgraderPlugin : IPlugin {
        void Upgrade(EventUpgraderContext ctx);
    }
}
