using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.Formatting;

[Priority(10)]
public sealed class SimpleErrorFormattingPlugin : FormattingPluginBase
{
    public SimpleErrorFormattingPlugin(JsonSerializerOptions jsonOptions, AppOptions options, ILoggerFactory loggerFactory) : base(jsonOptions, options, loggerFactory) { }

    private bool ShouldHandle(PersistentEvent ev)
    {
        return ev.IsError() && ev.HasSimpleError();
    }

    public override SummaryData? GetStackSummaryData(Stack stack)
    {
        if (stack.SignatureInfo.Count is 0 || !stack.SignatureInfo.ContainsKey("StackTrace"))
            return null;

        var data = new Dictionary<string, object>();
        if (stack.SignatureInfo.TryGetValue("ExceptionType", out string? value))
        {
            data.Add("Type", value.TypeName());
            data.Add("TypeFullName", value);
        }

        if (stack.SignatureInfo.TryGetValue("Path", out value))
            data.Add("Path", value);

        return new SummaryData { Id = stack.Id, TemplateKey = "stack-simple-summary", Data = data };
    }

    public override string? GetStackTitle(PersistentEvent ev)
    {
        if (!ShouldHandle(ev))
            return null;

        var error = ev.GetSimpleError(_jsonOptions);
        return error?.Message;
    }

    public override SummaryData? GetEventSummaryData(PersistentEvent ev)
    {
        if (!ShouldHandle(ev))
            return null;

        var error = ev.GetSimpleError(_jsonOptions);
        if (error is null)
            return null;

        var data = new Dictionary<string, object?> { { "Message", ev.Message } };
        AddUserIdentitySummaryData(data, ev.GetUserIdentity(_jsonOptions));

        if (!String.IsNullOrEmpty(error.Type))
        {
            data.Add("Type", error.Type.TypeName());
            data.Add("TypeFullName", error.Type);
        }

        var requestInfo = ev.GetRequestInfo(_jsonOptions);
        if (!String.IsNullOrEmpty(requestInfo?.Path))
            data.Add("Path", requestInfo.Path);

        return new SummaryData { Id = ev.Id, TemplateKey = "event-simple-summary", Data = data };
    }

    public override MailMessageData? GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression)
    {
        if (!ShouldHandle(ev))
            return null;

        var error = ev.GetSimpleError(_jsonOptions);
        if (error is null)
            return null;

        string? errorTypeName = null;
        if (!String.IsNullOrEmpty(error.Type))
            errorTypeName = error.Type.TypeName().Truncate(60);

        string errorType = !String.IsNullOrEmpty(errorTypeName) ? errorTypeName : "Error";
        string notificationType = String.Concat(errorType, " occurrence");
        if (isNew)
            notificationType = String.Concat(!isCritical ? "New " : "new ", errorType);
        else if (isRegression)
            notificationType = String.Concat(errorType, " regression");

        if (isCritical)
            notificationType = String.Concat("Critical ", notificationType);

        string subject = String.Concat(notificationType, ": ", error.Message).Truncate(120);
        var data = new Dictionary<string, object?> { { "Message", error.Message?.Truncate(60) } };
        if (!String.IsNullOrEmpty(errorTypeName))
            data.Add("Type", errorTypeName);

        var requestInfo = ev.GetRequestInfo(_jsonOptions);
        if (requestInfo is not null)
            data.Add("Url", requestInfo.GetFullPath(true, true, true));

        return new MailMessageData { Subject = subject, Data = data };
    }

    public override SlackMessage? GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression)
    {
        if (!ShouldHandle(ev))
            return null;

        var error = ev.GetSimpleError(_jsonOptions);
        if (error is null)
            return null;

        string? errorTypeName = null;
        if (!String.IsNullOrEmpty(error.Type))
            errorTypeName = error.Type.TypeName().Truncate(60);

        string errorType = !String.IsNullOrEmpty(errorTypeName) ? errorTypeName : "error";
        string notificationType = String.Concat(errorType, " occurrence");
        if (isNew)
            notificationType = String.Concat("new ", errorType);
        else if (isRegression)
            notificationType = String.Concat(errorType, " regression");

        if (isCritical)
            notificationType = String.Concat("critical ", notificationType);

        var attachment = new SlackMessage.SlackAttachment(ev, _jsonOptions)
        {
            Color = "#BB423F",
            Fields =
            [
                new() { Title = "Message", Value = error.Message?.Truncate(60) }
            ]
        };

        if (!String.IsNullOrEmpty(errorTypeName))
            attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Type", Value = errorTypeName });

        var lines = error.StackTrace?.SplitLines().ToList();
        if (lines is { Count: > 0 })
        {
            var frames = lines.Take(3).ToList();
            if (lines.Count > 3)
                frames.Add("...");

            attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Stack Trace", Value = $"```{String.Join("\n", frames)}```" });
        }

        AddDefaultSlackFields(ev, attachment.Fields);
        string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, error.Message?.Truncate(120))}*";
        return new SlackMessage(subject)
        {
            Attachments = [attachment]
        };
    }
}
