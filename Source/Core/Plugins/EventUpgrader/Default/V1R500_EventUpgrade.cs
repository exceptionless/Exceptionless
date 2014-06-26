using System;
using CodeSmith.Core.Component;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    /// <summary>
    /// Changed type of InstallDate from DateTime to DateTimeOffset
    /// </summary>
    [Priority(500)]
    public class V1R500EventUpgrade : IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(0, 9, 0, 500))
                return;

            var clientInfo = ctx.Document["ExceptionlessClientInfo"] as JObject;
            if (clientInfo == null || !clientInfo.HasValues || clientInfo["InstallDate"] == null)
                return;

            DateTimeOffset date; // This shouldn't hurt using DateTimeOffset to try and parse a date. It insures you won't lose any info.
            if (DateTimeOffset.TryParse(clientInfo["InstallDate"].ToString(), out date)) {
                clientInfo.Remove("InstallDate");
                clientInfo.Add("InstallDate", new JValue(date));
            } else
                clientInfo.Remove("InstallDate");
        }
    }
}