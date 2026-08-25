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
    private readonly IQueue<MailMessage> _queue;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly EmailAppUrlBuilder _appUrls;
    private readonly FormattingPluginManager _pluginManager;
    private readonly AppOptions _appOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ITextSerializer _serializer;
    private readonly ILogger _logger;

    public Mailer(IQueue<MailMessage> queue, FormattingPluginManager pluginManager, ITextSerializer serializer, AppOptions appOptions, TimeProvider timeProvider, ILogger<Mailer> logger)
        : this(queue, new RazorEmailTemplateRenderer(), pluginManager, serializer, appOptions, timeProvider, logger)
    {
    }

    public Mailer(IQueue<MailMessage> queue, IEmailTemplateRenderer templateRenderer, FormattingPluginManager pluginManager, ITextSerializer serializer, AppOptions appOptions, TimeProvider timeProvider, ILogger<Mailer> logger)
    {
        _queue = queue;
        _templateRenderer = templateRenderer;
        _appUrls = new EmailAppUrlBuilder(appOptions.BaseURL);
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
                new("Mark event as fixed", _appUrls.MarkStackFixed(ev.StackId)),
                new("Stop sending notifications for this event", _appUrls.IgnoreStack(ev.StackId)),
                new("Discard future event occurrences", _appUrls.DiscardStack(ev.StackId)),
                new("Change your notification settings for this project", _appUrls.ProjectNotifications(project.Id))
            ],
            new EmailAction("View Event Details", _appUrls.Event(ev.Id)));

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
            new EmailAction("View Organization", _appUrls.OrganizationDashboard(organization.Id)));

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
            new EmailAction("Join Organization", _appUrls.Signup(invite.Token)));

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
        string upgradeUrl = _appUrls.OrganizationUpgrade(organization.Id);
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
            _appUrls.OrganizationFrequent(organization.Id),
            learnMoreUrl,
            [
                new("View usage", _appUrls.OrganizationManage(organization.Id)),
                new("Change your notification settings", _appUrls.AccountNotifications())
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
            _appUrls.OrganizationBilling(organization.Id));

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
        string timelineUrl = _appUrls.ProjectTimeline(project.Id);
        string configureUrl = _appUrls.ProjectConfigure(project.Id);
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
            GetStackTemplateData(mostFrequent),
            GetStackTemplateData(newest),
            timelineUrl,
            configureUrl,
            _appUrls.OrganizationUpgrade(project.OrganizationId),
            _appUrls.ProjectMostFrequent(project.Id),
            _appUrls.ProjectNewest(project.Id),
            _appUrls.ProjectNotifications(project.Id));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

    private IReadOnlyCollection<StackSummary> GetStackTemplateData(IEnumerable<Stack>? stacks)
    {
        if (stacks is null)
        {
            return [];
        }

        return stacks.Select(stack => new StackSummary(
            stack.Title.Truncate(50),
            stack.GetTypeName()?.Truncate(50),
            stack.Status == StackStatus.Regressed,
            _appUrls.Stack(stack.Id))).ToArray();
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
            new EmailAction("Verify Address", _appUrls.VerifyEmail(user.VerifyEmailAddressToken)));

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
        var message = new UserPasswordResetEmail(
            subject,
            user.FullName,
            _appUrls.PasswordReset(user.PasswordResetToken, cancel: true),
            new EmailAction("Reset Password", _appUrls.PasswordReset(user.PasswordResetToken)));

        await QueueMessageAsync(new MailMessage
        {
            To = user.EmailAddress,
            Subject = subject,
            Body = await _templateRenderer.RenderAsync(message)
        }, template);
    }

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
