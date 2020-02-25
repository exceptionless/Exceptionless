using System;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(844)]
    public class V1R844EventUpgrade : PluginBase, IEventUpgraderPlugin {
        public V1R844EventUpgrade(AppOptions options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) { }

        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(1, 0, 0, 844))
                return;

            foreach (var doc in ctx.Documents) {

                if (!(doc["RequestInfo"] is JObject requestInfo) || !requestInfo.HasValues)
                    return;

                if (requestInfo["Cookies"] != null && requestInfo["Cookies"].HasValues) {
                    if (requestInfo["Cookies"] is JObject cookies)
                        cookies.Remove("");
                }

                if (requestInfo["Form"] != null && requestInfo["Form"].HasValues) {
                    if (requestInfo["Form"] is JObject form)
                        form.Remove("");
                }

                if (requestInfo["QueryString"] != null && requestInfo["QueryString"].HasValues) {
                    if (requestInfo["QueryString"] is JObject queryString)
                        queryString.Remove("");
                }
            }
        }
    }
}