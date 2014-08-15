using System;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public interface IEventUpgraderPlugin {
        void Upgrade(EventUpgraderContext ctx);
    }
}
