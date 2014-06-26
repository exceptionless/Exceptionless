using System;
using CodeSmith.Core.Component;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(850)]
    public class V1R850EventUpgrade : IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(0, 9, 0, 850))
                return;

            JObject current = ctx.Document;
            while (current != null) {
                var extendedData = ctx.Document["ExtendedData"] as JObject;
                if (extendedData != null) {
                    if (extendedData["ExtraExceptionProperties"] != null)
                        extendedData.Rename("ExtraExceptionProperties", "__ExceptionInfo");

                    if (extendedData["ExceptionInfo"] != null)
                        extendedData.Rename("ExceptionInfo", "__ExceptionInfo");

                    if (extendedData["TraceInfo"] != null)
                        extendedData.Rename("TraceInfo", "TraceLog");
                }

                current = current["Inner"] as JObject;
            }
        }
    }
}