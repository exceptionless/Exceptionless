using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(5)]
    public sealed class ManualStackingFormattingPlugin : FormattingPluginBase {
        public override string GetStackTitle(PersistentEvent ev) {
            var msi = ev.GetManualStackingInfo();
            return !String.IsNullOrWhiteSpace(msi?.Title) ? msi.Title : null;
        }
    }
}