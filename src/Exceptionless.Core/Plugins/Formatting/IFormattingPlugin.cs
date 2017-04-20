using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    public interface IFormattingPlugin : IPlugin {
        string GetStackTitle(PersistentEvent ev);
        SummaryData GetStackSummaryData(Stack stack);
        SummaryData GetEventSummaryData(PersistentEvent ev);
        Dictionary<string, object> GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression);
    }
}