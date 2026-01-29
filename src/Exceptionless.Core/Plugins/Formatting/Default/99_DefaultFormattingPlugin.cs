using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.Formatting;

[Priority(99)]
public sealed class DefaultFormattingPlugin : FormattingPluginBase
{
    public DefaultFormattingPlugin(JsonSerializerOptions jsonOptions, AppOptions options, ILoggerFactory loggerFactory) : base(jsonOptions, options, loggerFactory) { }

    public override string GetStackTitle(PersistentEvent ev)
    {
        if (String.IsNullOrWhiteSpace(ev.Message) && ev.IsError())
            return "Unknown Error";

        return ev.Message ?? ev.Source ?? $"{ev.Type} Event".TrimStart();
    }

    public override SummaryData GetStackSummaryData(Stack stack)
    {
        var data = new Dictionary<string, object> { { "Type", stack.Type } };

        if (stack.SignatureInfo.TryGetValue("Source", out string? value))
            data.Add("Source", value);

        return new SummaryData { Id = stack.Id, TemplateKey = "stack-summary", Data = data };
    }

    public override SummaryData GetEventSummaryData(PersistentEvent ev)
    {
        var data = new Dictionary<string, object?> {
                { "Message", GetStackTitle(ev) },
                { "Source", ev.Source },
                { "Type", ev.Type }
            };

        AddUserIdentitySummaryData(data, ev.GetUserIdentity(_jsonOptions));

        return new SummaryData { Id = ev.Id, TemplateKey = "event-summary", Data = data };
    }

    public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression)
    {
        string? messageOrSource = !String.IsNullOrEmpty(ev.Message) ? ev.Message : ev.Source;
        if (String.IsNullOrEmpty(messageOrSource))
            throw new ArgumentException("Event must contain message or source");

        string notificationType = "Occurrence event";
        if (isNew)
            notificationType = "New event";
        else if (isRegression)
            notificationType = "Regression event";

        if (isCritical)
            notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

        string subject = String.IsNullOrEmpty(messageOrSource)
            ? notificationType
            : String.Concat(notificationType, ": ", messageOrSource).Truncate(120);

        var data = new Dictionary<string, object?>();
        if (!String.IsNullOrEmpty(ev.Message))
            data.Add("Message", ev.Message.Truncate(60));

        if (!String.IsNullOrEmpty(ev.Source))
            data.Add("Source", ev.Source.Truncate(60));

        var requestInfo = ev.GetRequestInfo(_jsonOptions);
        if (requestInfo is not null)
            data.Add("Url", requestInfo.GetFullPath(true, true, true));

        return new MailMessageData { Subject = subject, Data = data };
    }

    public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression)
    {
        string? messageOrSource = !String.IsNullOrEmpty(ev.Message) ? ev.Message : ev.Source;
        if (String.IsNullOrEmpty(messageOrSource))
            throw new ArgumentException("Event must contain message or source");

        string notificationType = "Occurrence event";
        if (isNew)
            notificationType = "New event";
        else if (isRegression)
            notificationType = "Regression event";

        if (isCritical)
            notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

        var attachment = new SlackMessage.SlackAttachment(ev, _jsonOptions);
        if (!String.IsNullOrEmpty(ev.Message))
            attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Message", Value = ev.Message.Truncate(60) });

        if (!String.IsNullOrEmpty(ev.Source))
            attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Source", Value = ev.Source.Truncate(60) });

        AddDefaultSlackFields(ev, attachment.Fields);
        string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, messageOrSource.Truncate(120))}*";
        return new SlackMessage(subject)
        {
            Attachments = [attachment]
        };
    }
}
