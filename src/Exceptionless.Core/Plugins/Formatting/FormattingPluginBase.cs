using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Mail;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.Formatting;

public abstract class FormattingPluginBase : PluginBase, IFormattingPlugin
{
    protected readonly ITextSerializer _serializer;

    public FormattingPluginBase(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public virtual SummaryData? GetStackSummaryData(Stack stack)
    {
        return null;
    }

    public virtual SummaryData? GetEventSummaryData(PersistentEvent ev)
    {
        return null;
    }

    public virtual string? GetStackTitle(PersistentEvent ev)
    {
        return null;
    }

    public virtual MailMessageData? GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression)
    {
        return null;
    }

    public virtual SlackMessage? GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression)
    {
        return null;
    }

    protected void AddDefaultSlackFields(PersistentEvent ev, List<SlackMessage.SlackAttachmentFields> attachmentFields, bool includeUrl = true)
    {
        var requestInfo = ev.GetRequestInfo(_serializer, _logger);
        if (requestInfo is not null && includeUrl)
            attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Url", Value = requestInfo.GetFullPath(true, true, true) });

        if (ev.Tags is not null && ev.Tags.Count > 0)
            attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Tags", Value = String.Join(", ", ev.Tags), Short = true });

        decimal value = ev.Value.GetValueOrDefault();
        if (value != 0)
            attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Value", Value = value.ToString(), Short = true });

        string? version = ev.GetVersion();
        if (!String.IsNullOrEmpty(version))
            attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Version", Value = version, Short = true });

        var appUrls = new EmailAppUrlBuilder(_options.BaseURL);
        var actions = new List<string>
        {
            $"• {GetSlackEventUrl(ev.Id, "View Event")}",
            $"• <{appUrls.MarkStackFixed(ev.StackId)}|Mark event as fixed>",
            $"• <{appUrls.IgnoreStack(ev.StackId)}|Stop sending notifications for this event>",
            $"• <{appUrls.DiscardStack(ev.StackId)}|Discard future event occurrences>",
            $"• <{appUrls.ProjectIntegrations(ev.ProjectId)}|Change your notification settings for this project>"
        };

        attachmentFields.Add(new SlackMessage.SlackAttachmentFields { Title = "Other Actions", Value = String.Join("\n", actions) });
    }

    protected string GetSlackEventUrl(string eventId, string? message = null)
    {
        var parts = new List<string> { new EmailAppUrlBuilder(_options.BaseURL).Event(eventId) };
        if (!String.IsNullOrEmpty(message))
            parts.Add($"|{message.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")}");

        return $"<{String.Join(String.Empty, parts)}>";
    }

    protected void AddUserIdentitySummaryData(Dictionary<string, object?> data, UserInfo? identity)
    {
        if (identity is null)
            return;

        if (!String.IsNullOrEmpty(identity.Identity))
            data.Add("Identity", identity.Identity);

        if (!String.IsNullOrEmpty(identity.Name))
            data.Add("Name", identity.Name);
    }
}
