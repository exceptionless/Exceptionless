using System;
using Exceptionless.Core.Pipeline;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    /// <summary>
    /// Changed type of InstallDate from DateTime to DateTimeOffset
    /// </summary>
    [Priority(500)]
    public class V1R500EventUpgrade : PluginBase, IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(1, 0, 0, 500))
                return;

            foreach (var doc in ctx.Documents) {
                var clientInfo = doc["ExceptionlessClientInfo"] as JObject;
                if (clientInfo == null || !clientInfo.HasValues || clientInfo["InstallDate"] == null)
                    return;

                // This shouldn't hurt using DateTimeOffset to try and parse a date. It insures you won't lose any info.
                if (DateTimeOffset.TryParse(clientInfo["InstallDate"].ToString(), out DateTimeOffset date)) {
                    clientInfo.Remove("InstallDate");
                    clientInfo.Add("InstallDate", new JValue(date));
                } else {
                    clientInfo.Remove("InstallDate");
                }
            }
        }
    }
}