using System;
using System.Net.Mail;
using Exceptionless.Core.Queues;
using Exceptionless.Models;

namespace Exceptionless.Core.FormattingPlugins {
    public abstract class FormattingPluginBase : IFormattingPlugin {
        public virtual string GetStackSummaryHtml(Event ev) {
            return null;
        }

        public virtual string GetEventSummaryHtml(Event ev) {
            return null;
        }

        public virtual string GetStackTitle(Event ev) {
            return null;
        }

        public virtual MailMessage GetEventNotificationMailMessage(EventNotification model) {
            return null;
        }

        public virtual string GetEventViewName(Event ev) {
            return null;
        }
    }
}
