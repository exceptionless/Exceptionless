using System;
using System.Net.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    public interface IFormattingPlugin {
        string GetStackSummaryHtml(PersistentEvent ev);
        string GetEventSummaryHtml(PersistentEvent ev);
        string GetStackTitle(PersistentEvent ev);
        MailMessage GetEventNotificationMailMessage(EventNotification model);
        string GetEventViewName(PersistentEvent ev);
    }
}