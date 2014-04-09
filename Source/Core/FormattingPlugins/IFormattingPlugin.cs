using System;
using Exceptionless.Core.Queues;
using Exceptionless.Models;

namespace Exceptionless.Core.FormattingPlugins {
    public interface IFormattingPlugin {
        string GetEventSummaryHtml(Event ev);
        string GetStackTitle(Event ev);
        MailContent GetEventMailNotificationContent(EventNotification notification);
        string GetEventViewName(Event ev);
    }
}
