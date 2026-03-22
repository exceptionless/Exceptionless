using System.Text.Json.Nodes;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader;

/// <summary>
/// Changed type of InstallDate from DateTime to DateTimeOffset
/// </summary>
[Priority(500)]
public class V1R500EventUpgrade : PluginBase, IEventUpgraderPlugin
{
    public V1R500EventUpgrade(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public void Upgrade(EventUpgraderContext ctx)
    {
        if (ctx.Version > new Version(1, 0, 0, 500))
            return;

        foreach (var doc in ctx.Documents)
        {
            if (doc is not JsonObject docObj || docObj["ExceptionlessClientInfo"] is not JsonObject { Count: > 0 } clientInfo || clientInfo["InstallDate"] is null)
                continue;

            // This shouldn't hurt using DateTimeOffset to try and parse a date. It insures you won't lose any info.
            if (DateTimeOffset.TryParse(clientInfo["InstallDate"]?.ToString(), out var date))
            {
                clientInfo.Remove("InstallDate");
                // Format date as ISO 8601 with offset (matching Newtonsoft behavior)
                clientInfo.Add("InstallDate", JsonValue.Create(date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz")));
            }
            else
            {
                clientInfo.Remove("InstallDate");
            }
        }
    }
}
