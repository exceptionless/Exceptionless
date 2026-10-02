namespace Exceptionless.Core.Utility;

/// <summary>
/// Centralizes public application destinations used in notifications.
/// </summary>
internal sealed class AppUrlBuilder
{
    private readonly string _baseUrl;

    public AppUrlBuilder(string baseUrl)
    {
        _baseUrl = GetOrigin(baseUrl);
    }

    // Svelte serves at the root even when an installation retains a historical /next or hash base URL.
    public static string GetOrigin(string baseUrl) => new Uri(baseUrl).GetLeftPart(UriPartial.Authority);

    public string Event(string eventId) => Build($"event/{eventId}");

    public string Stack(string stackId) => Build($"stack/{stackId}");

    public string MarkStackFixed(string stackId) => Build($"stack/{stackId}/mark-fixed");

    public string IgnoreStack(string stackId) => Build($"stack/{stackId}/ignored");

    public string DiscardStack(string stackId) => Build($"stack/{stackId}/discarded");

    public string ProjectNotifications(string projectId) => Build($"account/notifications?project={Uri.EscapeDataString(projectId)}");

    public string ProjectIntegrations(string projectId) => Build($"project/{projectId}/integrations");

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
