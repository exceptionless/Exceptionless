using System.Text.Json.Nodes;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader;

[Priority(850)]
public class V1R850EventUpgrade : PluginBase, IEventUpgraderPlugin
{
    public V1R850EventUpgrade(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public void Upgrade(EventUpgraderContext ctx)
    {
        if (ctx.Version > new Version(1, 0, 0, 850))
            return;

        foreach (var doc in ctx.Documents.OfType<JsonObject>())
        {
            var current = doc;
            while (current is not null)
            {
                if (doc["ExtendedData"] is JsonObject extendedData)
                {
                    if (extendedData["ExtraExceptionProperties"] is not null)
                        extendedData.Rename("ExtraExceptionProperties", "__ExceptionInfo");

                    if (extendedData["ExceptionInfo"] is not null)
                        extendedData.Rename("ExceptionInfo", "__ExceptionInfo");

                    if (extendedData["TraceInfo"] is not null)
                        extendedData.Rename("TraceInfo", "TraceLog");
                }

                current = current["Inner"] as JsonObject;
            }
        }
    }
}
