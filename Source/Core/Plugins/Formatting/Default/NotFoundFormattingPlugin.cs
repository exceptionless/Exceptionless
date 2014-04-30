using System;
using System.Net.Mail;
using CodeSmith.Core.Component;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(20)]
    public class NotFoundFormattingPlugin : IFormattingPlugin {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsNotFound;
        }

        public string GetStackSummaryHtml(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return String.Format("<strong><abbr title=\"{0}\">404</abbr>:</strong> <a class=\"t8-default\" href=\"/stack/{1}\">{0}</a>", ev.Source, ev.StackId);
        }

        public string GetEventSummaryHtml(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return String.Format("<strong><abbr title=\"{0}\">404</abbr>:</strong> <a class=\"t8-default\" href=\"/event/{1}\">{0}</a>", ev.Source, ev.Id);
        }

        public string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return ev.Source;
        }

        public MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            throw new NotImplementedException();
        }

        public string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "NotFound";
        }
    }
}
