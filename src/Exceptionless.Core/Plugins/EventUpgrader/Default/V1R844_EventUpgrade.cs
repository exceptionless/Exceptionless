using System;
using Exceptionless.Core.Pipeline;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(844)]
    public class V1R844EventUpgrade : PluginBase, IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(1, 0, 0, 844))
                return;

            foreach (var doc in ctx.Documents) {
                var requestInfo = doc["RequestInfo"] as JObject;

                if (requestInfo == null || !requestInfo.HasValues)
                    return;

                if (requestInfo["Cookies"] != null && requestInfo["Cookies"].HasValues) {
                    var cookies = requestInfo["Cookies"] as JObject;
                    if (cookies != null)
                        cookies.Remove("");
                }

                if (requestInfo["Form"] != null && requestInfo["Form"].HasValues) {
                    var form = requestInfo["Form"] as JObject;
                    if (form != null)
                        form.Remove("");
                }

                if (requestInfo["QueryString"] != null && requestInfo["QueryString"].HasValues) {
                    var queryString = requestInfo["QueryString"] as JObject;
                    if (queryString != null)
                        queryString.Remove("");
                }
            }
        }
    }
}