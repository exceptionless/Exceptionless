using System;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(850)]
    public class V1R850EventUpgrade : PluginBase, IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(1, 0, 0, 850))
                return;

            foreach (var doc in ctx.Documents.OfType<JObject>()) {
                JObject current = doc;
                while (current != null) {
                    var extendedData = doc["ExtendedData"] as JObject;
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
}