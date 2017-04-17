using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(30)]
    public sealed class NotFoundFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsNotFound();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.NotFound))
                return null;

            return new SummaryData { TemplateKey = "stack-notfound-summary", Data = new Dictionary<string, object>() };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Unknown)";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var data = new Dictionary<string, object> { { "Source", ev.Source } };
            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            return new SummaryData { TemplateKey = "event-notfound-summary", Data = data };
        }

        public override Dictionary<string, object> GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            string notificationType = "Occurrence 404";
            if (model.IsNew)
                notificationType = "New 404";
            else if (model.IsRegression)
                notificationType = "Regression 404";

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

           var requestInfo = model.Event.GetRequestInfo();
            return new Dictionary<string, object> {
                { "Subject", String.Concat(notificationType, ": ", model.Event.Source.Truncate(120)) },
                { "BaseUrl", Settings.Current.BaseURL },
                { "Url", requestInfo?.GetFullPath(true, true, true) ?? model.Event.Source }
            };
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-NotFound";
        }
    }
}