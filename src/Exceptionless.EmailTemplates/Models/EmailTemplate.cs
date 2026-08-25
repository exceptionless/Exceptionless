namespace Exceptionless.EmailTemplates.Models;

public abstract record EmailTemplate(string Subject, EmailAction? Action = null, string? Preview = null);

public sealed record EmailAction(string Name, string Url);

public sealed record EmailLink(string Text, string Url);

public sealed record ContactRequestEmail(
    string Subject,
    string Name,
    string EmailAddress,
    string? Company,
    string RequestSubject,
    IReadOnlyCollection<string> MessageLines,
    string? ClientIpAddress,
    string? UserAgent,
    string? Referrer) : EmailTemplate(Subject);

public sealed record EventNoticeEmail(
    string Subject,
    string ProjectName,
    bool IsCritical,
    bool IsNew,
    bool IsRegression,
    int TotalOccurrences,
    IReadOnlyDictionary<string, string> Fields,
    EventUser? User,
    IReadOnlyCollection<EmailLink> OtherActions,
    EmailAction Action) : EmailTemplate(Subject, Action);

public sealed record EventUser(string? DisplayName, string? EmailUrl, string? Description);

public sealed record OrganizationAddedEmail(string Subject, EmailAction Action) : EmailTemplate(Subject, Action);

public sealed record OrganizationInvitedEmail(string Subject, EmailAction Action) : EmailTemplate(Subject, Action);

public sealed record OrganizationNoticeEmail(
    string Subject,
    string OrganizationName,
    bool IsOverMonthlyLimit,
    bool IsOverHourlyLimit,
    string ThrottledUntil,
    string UpgradeUrl,
    string FrequentEventsUrl,
    string LearnMoreUrl,
    IReadOnlyCollection<EmailLink> OtherActions)
    : EmailTemplate(Subject, new EmailAction("Upgrade Plan", UpgradeUrl));

public sealed record OrganizationPaymentFailedEmail(
    string Subject,
    string OrganizationName,
    string BillingUrl)
    : EmailTemplate(Subject, new EmailAction("Update Billing Information", BillingUrl));

public sealed record ProjectDailySummaryEmail(
    string Subject,
    string ProjectName,
    string StartDate,
    bool HasSubmittedEvents,
    double Count,
    double Unique,
    double New,
    double Fixed,
    int Blocked,
    bool IsFreePlan,
    IReadOnlyCollection<StackSummary> MostFrequent,
    IReadOnlyCollection<StackSummary> Newest,
    string TimelineUrl,
    string ConfigureUrl,
    string UpgradeUrl,
    string MostFrequentUrl,
    string NewestUrl,
    string NotificationSettingsUrl)
    : EmailTemplate(
        Subject,
        HasSubmittedEvents ? new EmailAction("View Timeline", TimelineUrl) : new EmailAction("Configure Project", ConfigureUrl));

public sealed record StackSummary(string Title, string? TypeName, bool IsRegressed, string Url);

public sealed record UserEmailVerifyEmail(string Subject, string UserFullName, EmailAction Action)
    : EmailTemplate(Subject, Action);

public sealed record UserPasswordResetEmail(string Subject, string UserFullName, string CancelUrl, EmailAction Action)
    : EmailTemplate(Subject, Action);
