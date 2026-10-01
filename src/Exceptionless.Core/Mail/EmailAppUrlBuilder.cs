namespace Exceptionless.Core.Mail;

/// <summary>
/// Centralizes public application destinations used in email.
/// </summary>
internal sealed class EmailAppUrlBuilder
{
    private readonly string _baseUrl;

    public EmailAppUrlBuilder(string baseUrl)
    {
        // Older installations used a hash-router base URL.
        _baseUrl = baseUrl.TrimEnd('/');
        if (_baseUrl.EndsWith("/#!", StringComparison.Ordinal) || _baseUrl.EndsWith("/#", StringComparison.Ordinal))
            _baseUrl = _baseUrl[.._baseUrl.LastIndexOf('/')];
    }

    public string Event(string eventId) => Build($"event/{eventId}");

    public string Stack(string stackId) => Build($"stack/{stackId}");

    public string ProjectNotifications(string projectId) => Build($"account/notifications?project={Uri.EscapeDataString(projectId)}");

    public string OrganizationDashboard(string organizationId) => Build($"organization/{organizationId}/dashboard");

    public string Signup(string token) => Build($"signup?token={Uri.EscapeDataString(token)}");

    public string OrganizationUpgrade(string organizationId) => Build($"organization/{organizationId}/billing?changePlan=true");

    public string OrganizationFrequent(string organizationId) => Build($"organization/{organizationId}/dashboard?view=stacks");

    public string OrganizationManage(string organizationId) => Build($"organization/{organizationId}/usage");

    public string OrganizationBilling(string organizationId) => Build($"organization/{organizationId}/billing");

    public string ProjectTimeline(string projectId) => Build($"project/{projectId}/dashboard?type=error");

    public string ProjectConfigure(string projectId) => Build($"project/{projectId}/configure");

    public string ProjectMostFrequent(string projectId) => Build($"project/{projectId}/dashboard?type=error&view=stacks");

    public string ProjectNewest(string projectId) => Build($"project/{projectId}/dashboard?type=error&view=stacks");

    public string AccountNotifications() => Build("account/notifications");

    public string VerifyEmail(string token) => Build($"account/verify?token={Uri.EscapeDataString(token)}");

    public string PasswordReset(string token, bool cancel = false)
    {
        string url = Build($"reset-password/{Uri.EscapeDataString(token)}");
        return cancel ? $"{url}?cancel=true" : url;
    }

    private string Build(string relativeUrl) => $"{_baseUrl}/{relativeUrl.TrimStart('/')}";
}
