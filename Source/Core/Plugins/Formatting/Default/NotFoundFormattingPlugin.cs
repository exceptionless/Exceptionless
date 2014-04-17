using System;
using System.Net.Mail;
using CodeSmith.Core.Component;
using Exceptionless.Core.Queues;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(20)]
    public class NotFoundFormattingPlugin : IFormattingPlugin {
        private bool ShouldHandle(Event ev) {
            return ev.IsNotFound;
        }

        public string GetStackSummaryHtml(Event ev) {
            if (!ShouldHandle(ev))
                return null;

            return String.Format("<strong><abbr title=\"{0}\">404</abbr>:</strong> <a class=\"t8-default\" href=\"/stack/{1}\">{0}</a>", ev.Source, ev.StackId);
        }

        public string GetEventSummaryHtml(Event ev) {
            if (!ShouldHandle(ev))
                return null;

            return String.Format("<strong><abbr title=\"{0}\">404</abbr>:</strong> <a class=\"t8-default\" href=\"/event/{1}\">{0}</a>", ev.Source, ev.Id);
        }

        public string GetStackTitle(Event ev) {
            if (!ShouldHandle(ev))
                return null;

            return ev.Source;
        }

        public MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            throw new NotImplementedException();
        }

        public string GetEventViewName(Event ev) {
            if (!ShouldHandle(ev))
                return null;

            return "NotFound";
        }
    }
}
