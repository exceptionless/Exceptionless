using System;
using System.Net.Mail;
using Exceptionless.Core.Queues;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    public interface IFormattingPlugin {
        string GetStackSummaryHtml(Event ev);
        string GetEventSummaryHtml(Event ev);
        string GetStackTitle(Event ev);
        MailMessage GetEventNotificationMailMessage(EventNotification model);
        string GetEventViewName(Event ev);
    }
}