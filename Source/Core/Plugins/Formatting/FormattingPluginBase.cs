using System;
using System.Net.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;
using MailMessage = System.Net.Mail.MailMessage;

namespace Exceptionless.Core.Plugins.Formatting {
    public abstract class FormattingPluginBase : IFormattingPlugin {
        public virtual string GetStackSummaryHtml(PersistentEvent ev) {
            return null;
        }

        public virtual string GetEventSummaryHtml(PersistentEvent ev) {
            return null;
        }

        public virtual string GetStackTitle(PersistentEvent ev) {
            return null;
        }

        public virtual MailMessage GetEventNotificationMailMessage(EventNotification model) {
            return null;
        }

        public virtual string GetEventViewName(PersistentEvent ev) {
            return null;
        }
    }
}