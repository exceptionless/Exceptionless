namespace Exceptionless.Core.Mail;

/// <summary>
/// Centralizes the existing email destinations. Update these mappings when the Svelte UI moves to the application root.
/// </summary>
internal sealed class EmailAppUrlBuilder
{
    private readonly string _baseUrl;

    public EmailAppUrlBuilder(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string Event(string eventId) => Build($"event/{eventId}");

    public string Stack(string stackId) => Build($"stack/{stackId}");

    public string MarkStackFixed(string stackId) => Build($"stack/{stackId}/mark-fixed");

    public string IgnoreStack(string stackId) => Build($"stack/{stackId}/ignored");

    public string DiscardStack(string stackId) => Build($"stack/{stackId}/discarded");

    public string ProjectNotifications(string projectId) => Build($"account/manage?projectId={Uri.EscapeDataString(projectId)}&tab=notifications");

    public string OrganizationDashboard(string organizationId) => Build($"organization/{organizationId}/dashboard");

    public string Signup(string token) => Build($"signup?token={Uri.EscapeDataString(token)}");

    public string OrganizationUpgrade(string organizationId) => Build($"organization/{organizationId}/upgrade");

    public string OrganizationFrequent(string organizationId) => Build($"organization/{organizationId}/frequent");

    public string OrganizationManage(string organizationId) => Build($"organization/{organizationId}/manage");

    public string OrganizationBilling(string organizationId) => Build($"organization/{organizationId}/manage?tab=billing");

    public string ProjectTimeline(string projectId) => Build($"project/{projectId}/error/timeline");

    public string ProjectConfigure(string projectId) => Build($"project/{projectId}/configure");

    public string ProjectMostFrequent(string projectId) => Build($"project/{projectId}/error/frequent");

    public string ProjectNewest(string projectId) => Build($"project/{projectId}/error/new");

    public string AccountNotifications() => Build("account/manage?tab=notifications");

    public string VerifyEmail(string token) => Build($"account/verify?token={Uri.EscapeDataString(token)}");

    public string PasswordReset(string token, bool cancel = false)
    {
        string url = Build($"reset-password/{Uri.EscapeDataString(token)}");
        return cancel ? $"{url}?cancel=true" : url;
    }

    private string Build(string relativeUrl) => $"{_baseUrl}/{relativeUrl.TrimStart('/')}";
}
