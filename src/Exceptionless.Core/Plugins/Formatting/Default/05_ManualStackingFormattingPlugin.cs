using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.Formatting;

[Priority(5)]
public sealed class ManualStackingFormattingPlugin : FormattingPluginBase
{
    public ManualStackingFormattingPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(serializer, options, loggerFactory) { }

    public override string? GetStackTitle(PersistentEvent ev)
    {
        var msi = ev.GetManualStackingInfo(_serializer);
        return !String.IsNullOrWhiteSpace(msi?.Title) ? msi.Title : null;
    }
}
