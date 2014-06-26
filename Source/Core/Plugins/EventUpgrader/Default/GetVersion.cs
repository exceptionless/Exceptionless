using System;
using System.Linq;
using CodeSmith.Core.Component;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(0)]
    public class GetVersion : IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (!ctx.Document.HasValues) {
                ctx.Version = new Version();
                return;
            }

            var clientInfo = ctx.Document["ExceptionlessClientInfo"] as JObject;
            if (clientInfo == null || !clientInfo.HasValues || clientInfo["Version"] == null) {
                ctx.Version = new Version();
                return;
            }

            if (clientInfo["Version"].ToString().Contains(" ")) {
                string version = clientInfo["Version"].ToString().Split(' ').First();
                ctx.Version = new Version(version);
                return;
            }

            if (clientInfo["Version"].ToString().Contains("-")) {
                string version = clientInfo["Version"].ToString().Split('-').First();
                ctx.Version = new Version(version);
                return;
            }

            // old version format                
            ctx.Version = new Version(clientInfo["Version"].ToString());
        }
    }
}