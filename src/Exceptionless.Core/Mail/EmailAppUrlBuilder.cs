namespace Exceptionless.Core.Mail;

/// <summary>
/// Centralizes notification destinations in the Svelte application.
/// </summary>
internal sealed class EmailAppUrlBuilder
{
    private readonly string _baseUrl;

    public EmailAppUrlBuilder(string baseUrl)
    {
        _baseUrl = new Uri(baseUrl).GetLeftPart(UriPartial.Authority);
    }

    public string Event(string eventId) => Build($"event/{eventId}");

    public string Stack(string stackId) => Build($"stack/{stackId}");

    public string MarkStackFixed(string stackId) => Build($"stack/{stackId}?action=fixed");

    public string IgnoreStack(string stackId) => Build($"stack/{stackId}?action=ignored");

    public string DiscardStack(string stackId) => Build($"stack/{stackId}?action=discarded");

    public string ProjectNotifications(string projectId) => Build($"account/notifications?project={Uri.EscapeDataString(projectId)}");

    public string ProjectIntegrations(string projectId) => Build($"project/{projectId}/integrations");

    public string OrganizationDashboard(string organizationId) => Build($"event?organization={Uri.EscapeDataString(organizationId)}");

    public string Signup(string token) => Build($"signup?token={Uri.EscapeDataString(token)}");

    public string OrganizationUpgrade(string organizationId) => Build($"organization/{organizationId}/billing?changePlan=true");

    public string OrganizationFrequent(string organizationId) => Build($"stack?organization={Uri.EscapeDataString(organizationId)}&type=error");

    public string OrganizationUsage(string organizationId) => Build($"organization/{organizationId}/usage");

    public string OrganizationBilling(string organizationId) => Build($"organization/{organizationId}/billing");

    public string ProjectTimeline(string projectId, string organizationId) => Build($"event?organization={Uri.EscapeDataString(organizationId)}&project={Uri.EscapeDataString(projectId)}&type=error");

    public string ProjectConfigure(string projectId) => Build($"project/{projectId}/configure");

    public string ProjectMostFrequent(string projectId, string organizationId) => Build($"stack?organization={Uri.EscapeDataString(organizationId)}&project={Uri.EscapeDataString(projectId)}&type=error");

    public string ProjectNewest(string projectId, string organizationId) => Build($"stack?organization={Uri.EscapeDataString(organizationId)}&project={Uri.EscapeDataString(projectId)}&mode=stack_new&type=error");

    public string AccountNotifications() => Build("account/notifications");

    public string VerifyEmail(string token) => Build($"account/verify?token={Uri.EscapeDataString(token)}");

    public string PasswordReset(string token, bool cancel = false)
    {
        string url = Build($"reset-password/{Uri.EscapeDataString(token)}");
        return cancel ? $"{url}?cancel=true" : url;
    }

    private string Build(string relativeUrl) => $"{_baseUrl}/{relativeUrl.TrimStart('/')}";
}
