using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    public interface IFormattingPlugin : IPlugin {
        string GetStackTitle(PersistentEvent ev);
        string GetEventViewName(PersistentEvent ev);
        SummaryData GetStackSummaryData(Stack stack);
        SummaryData GetEventSummaryData(PersistentEvent ev);
        Dictionary<string, object> GetEventNotificationMailMessage(EventNotification model);
    }
}