using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Plugins.Formatting {
    public abstract class FormattingPluginBase : PluginBase, IFormattingPlugin {
        public virtual SummaryData GetStackSummaryData(Stack stack) {
            return null;
        }

        public virtual SummaryData GetEventSummaryData(PersistentEvent ev) {
            return null;
        }

        public virtual string GetStackTitle(PersistentEvent ev) {
            return null;
        }

        public virtual MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            return null;
        }

        public virtual SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            return null;
        }

        protected void AddDefaultSlackFields(PersistentEvent ev, List<SlackMessage.SlackAttachmentFields> attachmentFields, bool includeUrl = true) {
            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null && includeUrl)
                attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Url", Value = requestInfo.GetFullPath(true, true, true) });

            if (ev.Tags.Count > 0)
                attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Tags", Value = String.Join(", ", ev.Tags), Short = true });

            if (ev.Value.GetValueOrDefault() != 0)
                attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Value", Value = ev.Value.ToString(), Short = true });

            string version = ev.GetVersion();
            if (!String.IsNullOrEmpty(version))
                attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Version", Value = version, Short = true });

            var actions = new List<string> { $"• {GetSlackEventUrl(ev.Id, "View Event")}" };
            if (ev.Type == Event.KnownTypes.Error || ev.Type == Event.KnownTypes.NotFound)
                actions.Add($"• <{Settings.Current.BaseURL}/stack/{ev.StackId}/mark-fixed|Mark event as fixed>");
            actions.Add($"• <{Settings.Current.BaseURL}/stack/{ev.StackId}/stop-notifications|Stop sending notifications for this event>");
            actions.Add($"• <{Settings.Current.BaseURL}/project/{ev.ProjectId}/manage?tab=integrations|Change your notification settings for this project>");

            attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Other Actions", Value = String.Join("\n", actions) });
        }

        protected string GetSlackEventUrl(string eventId, string message = null) {
            var parts = new List<string> { $"{Settings.Current.BaseURL}/event/{eventId}" };
            if (!String.IsNullOrEmpty(message))
                parts.Add($"|{message.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")}");

            return $"<{String.Join(String.Empty, parts)}>";
        }

        protected void AddUserIdentitySummaryData(Dictionary<string, object> data, UserInfo identity) {
            if (identity == null)
                return;

            if (!String.IsNullOrEmpty(identity.Identity))
                data.Add("Identity", identity.Identity);

            if (!String.IsNullOrEmpty(identity.Name))
                data.Add("Name", identity.Name);
        }
    }
}