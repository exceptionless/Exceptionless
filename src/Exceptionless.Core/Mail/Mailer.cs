using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.DateTimeExtensions;
using Exceptionless.EmailTemplates;
using Exceptionless.EmailTemplates.Models;
using Foundatio.Queues;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Mail;

public class Mailer : IMailer
{
    private const string SvelteAppPathPrefix = "next/";
    private readonly IQueue<MailMessage> _queue;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly FormattingPluginManager _pluginManager;
    private readonly AppOptions _appOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ITextSerializer _serializer;
    private readonly ILogger _logger;

    public Mailer(IQueue<MailMessage> queue, IEmailTemplateRenderer templateRenderer, FormattingPluginManager pluginManager, ITextSerializer serializer, AppOptions appOptions, TimeProvider timeProvider, ILogger<Mailer> logger)
    {
        _queue = queue;
        _templateRenderer = templateRenderer;
        _pluginManager = pluginManager;
        _appOptions = appOptions;
        _timeProvider = timeProvider;
        _serializer = serializer;
        _logger = logger;
    }

    public async Task<bool> SendContactRequestAsync(string name, string emailAddress, string? company, string? subject, string message, string? clientIpAddress, string? userAgent, string? referrer)
    {
        string? contactEmailAddress = _appOptions.EmailOptions.ContactEmailAddress;
        if (String.IsNullOrWhiteSpace(contactEmailAddress))
        {
            _logger.LogWarning("Contact request mail was not sent: ContactEmailAddress is not configured");
            return false;
        }

        string requestSubject = String.IsNullOrWhiteSpace(subject)
            ? "Website contact request"
            : subject.Trim().StripInvisible().Truncate(100);
        string mailSubject = $"[Contact] {requestSubject}";
        const string template = "contact-request";
        string body = await _templateRenderer.RenderAsync(new ContactRequestEmail(
            mailSubject,
            name.Trim(),
            emailAddress.Trim(),
            company?.Trim(),
            requestSubject,
            message.SplitLines().ToArray(),
            clientIpAddress,
            userAgent,
            referrer));

        string? messageId = await QueueMessageAsync(new MailMessage
        {
            To = contactEmailAddress,
            ReplyTo = emailAddress.Trim(),
            Subject = mailSubject,
            Body = body
        }, template);
        return !String.IsNullOrEmpty(messageId);
    }

    public async Task<bool> SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences)
    {
        bool isCritical = ev.IsCritical();
        var result = _pluginManager.GetEventNotificationMailMessageData(ev, isCritical, isNew, isRegression);
        if (result is null || result.Data.Count == 0)
        {
            _logger.LogWarning("Unable to create event notification mail message for event \"{UserId}\". User: \"{EmailAddress}\"", ev.Id, user.EmailAddress);
            return false;
        }

        if (String.IsNullOrEmpty(result.Subject))
        {
            result.Subject = ev.Message ?? ev.Source ?? "(Global)";
        }

        AddDefaultFields(ev, result.Data);

        const string template = "event-notice";
        string stackUrl = GetAppUrl($"project/{project.Id}/stacks/{ev.StackId}");
        var message = new EventNoticeEmail(
            result.Subject,
            project.Name,
            isCritical,
            isNew,
            isRegression,
            totalOccurrences,
            result.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? String.Empty),
            GetEventUser(ev),
            [
                new("Mark event as fixed", stackUrl),
                new("Stop sending notifications for this event", stackUrl),
                new("Discard future event occurrences", stackUrl),
                new("Change your notification settings for this project", GetAppUrl($"account/notifications?project={Uri.EscapeDataString(project.Id)}"))
            ],
            new EmailAction("View Event Details", GetAppUrl($"event/{ev.Id}")));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = $"[{project.Name}] {result.Subject}",
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
        return true;
    }

    private EventUser? GetEventUser(PersistentEvent ev)
    {
        var ud = ev.GetUserDescription(_serializer, _logger);
        var ui = ev.GetUserIdentity(_serializer, _logger);

        string? displayName = null;
        if (!String.IsNullOrEmpty(ui?.Identity))
        {
            displayName = ui.Identity;
        }

        if (!String.IsNullOrEmpty(ui?.Name))
        {
            displayName = ui.Name;
        }

        if (!String.IsNullOrEmpty(displayName) && !String.IsNullOrEmpty(ud?.EmailAddress))
        {
            displayName = $"{displayName} ({ud.EmailAddress})";
        }
        else if (!String.IsNullOrEmpty(ui?.Identity) && !String.IsNullOrEmpty(ui.Name))
        {
            displayName = $"{ui.Name} ({ui.Identity})";
        }

        if (ud is null && ui is null)
        {
            return null;
        }

        string? emailUrl = !String.IsNullOrEmpty(ud?.EmailAddress)
            ? BuildMailtoUrl(ud.EmailAddress, ud.Description)
            : null;

        return new EventUser(displayName, emailUrl, ud?.Description);
    }

    private static string BuildMailtoUrl(string emailAddress, string? body)
    {
        string href = $"mailto:{Uri.EscapeDataString(emailAddress)}";
        return String.IsNullOrEmpty(body) ? href : $"{href}?body={Uri.EscapeDataString(body)}";
    }

    private static void AddDefaultFields(PersistentEvent ev, Dictionary<string, object?> data)
    {
        if (ev.Tags?.Count > 0)
        {
            data["Tags"] = String.Join(", ", ev.Tags);
        }

        decimal value = ev.Value.GetValueOrDefault();
        if (value != 0)
        {
            data["Value"] = value;
        }

        string? version = ev.GetVersion();
        if (!String.IsNullOrEmpty(version))
        {
            data["Version"] = version;
        }
    }

    public async Task SendOrganizationAddedAsync(User sender, Organization organization, User user)
    {
        const string template = "organization-added";
        string subject = $"{sender.FullName} added you to the organization \"{organization.Name}\" on Exceptionless";
        var message = new OrganizationAddedEmail(
            subject,
            new EmailAction("View Organization", GetAppUrl($"organization/{organization.Id}/manage")));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    public async Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite)
    {
        const string template = "organization-invited";
        string subject = $"{sender.FullName} invited you to join the organization \"{organization.Name}\" on Exceptionless";
        var message = new OrganizationInvitedEmail(
            subject,
            new EmailAction("Join Organization", GetAppUrl($"signup?token={Uri.EscapeDataString(invite.Token)}")));

        await QueueMessageAsync(new MailMessage
        {
            To = invite.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    public async Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit)
    {
        const string template = "organization-notice";
        string subject = isOverMonthlyLimit
                ? $"[{organization.Name}] Monthly plan limit exceeded."
                : $"[{organization.Name}] Events are currently being throttled.";
        string upgradeUrl = GetAppUrl($"organization/{organization.Id}/billing?changePlan=true");
        string learnMoreUrl = isOverMonthlyLimit
            ? "https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-what-happens-if-the-organization-plan-limit-is-reached"
            : "https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-why-is-my-organization-throttled";
        var message = new OrganizationNoticeEmail(
            subject,
            organization.Name,
            isOverMonthlyLimit,
            isOverHourlyLimit,
            _timeProvider.GetUtcNow().UtcDateTime.StartOfHour().AddHours(1).ToShortTimeString(),
            upgradeUrl,
            GetAppUrl("stack?status=open,regressed"),
            learnMoreUrl,
            [
                new("View usage", GetAppUrl($"organization/{organization.Id}/usage")),
                new("Change your notification settings", GetAppUrl("account/notifications"))
            ]);

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    public async Task SendOrganizationPaymentFailedAsync(User owner, Organization organization)
    {
        const string template = "organization-payment-failed";
        string subject = $"[{organization.Name}] Payment failed! Update billing information to avoid service interruption!";
        var message = new OrganizationPaymentFailedEmail(
            subject,
            organization.Name,
            GetAppUrl($"organization/{organization.Id}/billing"));

        await QueueMessageAsync(new MailMessage
        {
            To = owner.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    public async Task SendProjectDailySummaryAsync(User user, Project project, IEnumerable<Stack>? mostFrequent, IEnumerable<Stack>? newest, DateTime startDate, bool hasSubmittedEvents, double count, double uniqueCount, double newCount, double fixedCount, int blockedCount, int tooBigCount, bool isFreePlan)
    {
        const string template = "project-daily-summary";
        string subject = $"[{project.Name}] Summary for {startDate.ToLongDateString()}";
        string timelineUrl = GetAppUrl($"event?project={Uri.EscapeDataString(project.Id)}&type=error");
        string configureUrl = GetAppUrl($"project/{project.Id}/configure");
        var message = new ProjectDailySummaryEmail(
            subject,
            project.Name,
            startDate.ToLongDateString(),
            hasSubmittedEvents,
            count,
            uniqueCount,
            newCount,
            fixedCount,
            blockedCount,
            isFreePlan,
            GetStackTemplateData(project.Id, mostFrequent),
            GetStackTemplateData(project.Id, newest),
            timelineUrl,
            configureUrl,
            GetAppUrl($"organization/{project.OrganizationId}/billing?changePlan=true"),
            GetAppUrl($"project/{project.Id}/stacks?sort=-total"),
            GetAppUrl($"project/{project.Id}/stacks?sort=-first"),
            GetAppUrl($"account/notifications?project={Uri.EscapeDataString(project.Id)}"));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    private IReadOnlyCollection<StackSummary> GetStackTemplateData(string projectId, IEnumerable<Stack>? stacks)
    {
        if (stacks is null)
        {
            return [];
        }

        return stacks.Select(stack => new StackSummary(
            stack.Title.Truncate(50),
            stack.GetTypeName()?.Truncate(50),
            stack.Status == StackStatus.Regressed,
            GetAppUrl($"project/{projectId}/stacks/{stack.Id}"))).ToArray();
    }

    public async Task SendUserEmailVerifyAsync(User user)
    {
        if (String.IsNullOrEmpty(user?.VerifyEmailAddressToken))
        {
            return;
        }

        const string template = "user-email-verify";
        const string subject = "Exceptionless Account Confirmation";
        var message = new UserEmailVerifyEmail(
            subject,
            user.FullName,
            new EmailAction("Verify Address", GetAppUrl($"account/verify?token={Uri.EscapeDataString(user.VerifyEmailAddressToken)}")));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    public async Task SendUserPasswordResetAsync(User user)
    {
        if (String.IsNullOrEmpty(user?.PasswordResetToken))
        {
            return;
        }

        const string template = "user-password-reset";
        const string subject = "Exceptionless Password Reset";
        string resetUrl = GetAppUrl($"reset-password/{Uri.EscapeDataString(user.PasswordResetToken)}");
        var message = new UserPasswordResetEmail(
            subject,
            user.FullName,
            $"{resetUrl}?cancel=true",
            new EmailAction("Reset Password", resetUrl));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    private string GetAppUrl(string relativeUrl) => $"{_appOptions.BaseURL.TrimEnd('/')}/{SvelteAppPathPrefix}{relativeUrl.TrimStart('/')}";

    private Task<string?> QueueMessageAsync(MailMessage message, string metricsName)
    {
        if (!CleanAddresses(message))
        {
            return Task.FromResult<string?>(null);
        }

        AppDiagnostics.Counter($"mailer.{metricsName}");
        return _queue.EnqueueAsync(message);
    }

    private bool CleanAddresses(MailMessage message)
    {
        if (_appOptions.AppMode == AppMode.Production)
        {
            return true;
        }

        string address = message.To.ToLowerInvariant();
        if (_appOptions.EmailOptions.AllowedOutboundAddresses.Any(address.Contains))
        {
            return true;
        }

        if (String.IsNullOrEmpty(_appOptions.EmailOptions.TestEmailAddress))
        {
            _logger.LogWarning("Mail to {EmailAddress} dropped: TestEmailAddress is not configured", message.To);
            return false;
        }

        message.Subject = $"[{message.To}] {message.Subject}".StripInvisible();
        message.To = _appOptions.EmailOptions.TestEmailAddress;
        return true;
    }
}
