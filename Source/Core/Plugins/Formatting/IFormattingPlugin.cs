using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    public interface IFormattingPlugin {
        string GetStackTitle(PersistentEvent ev);
        string GetEventViewName(PersistentEvent ev);
        SummaryData GetStackSummaryData(Stack stack);
        SummaryData GetEventSummaryData(PersistentEvent ev);
        MailMessage GetEventNotificationMailMessage(EventNotification model);
    }
}