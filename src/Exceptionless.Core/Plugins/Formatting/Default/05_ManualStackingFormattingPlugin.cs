using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(5)]
    public sealed class ManualStackingFormattingPlugin : FormattingPluginBase {
        public ManualStackingFormattingPlugin(IOptions<AppOptions> options) : base(options) { }

        public override string GetStackTitle(PersistentEvent ev) {
            var msi = ev.GetManualStackingInfo();
            return !String.IsNullOrWhiteSpace(msi?.Title) ? msi.Title : null;
        }
    }
}