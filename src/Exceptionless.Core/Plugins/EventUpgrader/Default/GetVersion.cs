using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader;

[Priority(0)]
public class GetVersion : PluginBase, IEventUpgraderPlugin
{
    public GetVersion(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public void Upgrade(EventUpgraderContext ctx)
    {
        if (ctx.Version is not null)
            return;

        if (ctx.Documents.Count == 0 || !ctx.Documents.First().HasValues)
        {
            ctx.Version = new Version();
            return;
        }

        var doc = ctx.Documents.First();
        if (!(doc["ExceptionlessClientInfo"] is JObject { HasValues: true } clientInfo) || clientInfo["Version"] is null)
        {
            ctx.Version = new Version();
            return;
        }

        string? version = clientInfo.GetPropertyStringValue("Version");
        if (String.IsNullOrEmpty(version))
        {
            ctx.Version = new Version();
            return;
        }

        if (version.Contains(" "))
        {
            ctx.Version = new Version(version.Split(' ').First());
            return;
        }

        if (version.Contains("-"))
        {
            ctx.Version = new Version(version.Split('-').First());
            return;
        }

        // old version format
        ctx.Version = new Version(version);
    }
}
