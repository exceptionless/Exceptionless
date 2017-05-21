using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    public interface IFormattingPlugin : IPlugin {
        string GetStackTitle(PersistentEvent ev);
        SummaryData GetStackSummaryData(Stack stack);
        SummaryData GetEventSummaryData(PersistentEvent ev);
        MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression);
        SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression);
    }
}