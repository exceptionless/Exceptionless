using System.Text.Json.Nodes;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader;

[Priority(844)]
public class V1R844EventUpgrade : PluginBase, IEventUpgraderPlugin
{
    public V1R844EventUpgrade(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public void Upgrade(EventUpgraderContext ctx)
    {
        if (ctx.Version > new Version(1, 0, 0, 844))
            return;

        foreach (var doc in ctx.Documents)
        {
            if (doc is not JsonObject docObj || docObj["RequestInfo"] is not JsonObject { Count: > 0 } requestInfo)
                return;

            if (requestInfo["Cookies"] is JsonObject { Count: > 0 } cookies)
            {
                cookies.Remove("");
            }

            if (requestInfo["Form"] is JsonObject { Count: > 0 } form)
            {
                form.Remove("");
            }

            if (requestInfo["QueryString"] is JsonObject { Count: > 0 } queryString)
            {
                queryString.Remove("");
            }
        }
    }
}
