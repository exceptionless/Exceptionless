using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantAccessService(
    AppOptions appOptions,
    BillingPlans billingPlans,
    IOrganizationRepository organizationRepository)
{
    public async Task<AssistantAccessDecision> GetAccessAsync(HttpRequest request, string? organizationId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configurationDecision = EvaluateConfiguration(appOptions);
        if (configurationDecision is not null)
            return configurationDecision;

        if (String.IsNullOrWhiteSpace(organizationId))
            return AssistantAccessDecision.Unavailable(AssistantAccessReason.OrganizationRequired, "Select an organization to use Exie.");

        organizationId = organizationId.Trim();
        if (!request.CanAccessOrganization(organizationId))
            return AssistantAccessDecision.Unavailable(AssistantAccessReason.OrganizationNotAccessible, "You do not have access to this organization.");

        var organization = await organizationRepository.GetByIdAsync(organizationId, options => options.Cache());
        if (organization is null)
            return AssistantAccessDecision.Unavailable(AssistantAccessReason.OrganizationNotAccessible, "The selected organization could not be found.");

        if (organization.IsSuspended)
            return AssistantAccessDecision.Unavailable(AssistantAccessReason.OrganizationNotAccessible, "The selected organization is suspended.");

        if (appOptions.AppMode == AppMode.Development)
            return AssistantAccessDecision.Available(billingPlans.UnlimitedPlan.Assistant!);

        return EvaluatePlan(billingPlans.GetPlan(organization.PlanId)?.Assistant, billingPlans.MediumPlan.Id);
    }

    internal static AssistantAccessDecision? EvaluateConfiguration(AppOptions appOptions)
    {
        if (!appOptions.AssistantOptions.Enabled)
            return AssistantAccessDecision.Unavailable(AssistantAccessReason.Disabled, "Exie is disabled.", enabled: false);

        if (!appOptions.AssistantOptions.IsConfigured)
            return AssistantAccessDecision.Unavailable(AssistantAccessReason.NotConfigured, "Exie is not configured.", enabled: false);

        return null;
    }

    internal static AssistantAccessDecision EvaluatePlan(AssistantPlanOptions? planOptions, string minimumPlanId) => planOptions is not null
        ? AssistantAccessDecision.Available(planOptions)
        : AssistantAccessDecision.Unavailable(
            AssistantAccessReason.UpgradeRequired,
            "Exie is available on Medium plans and higher.",
            upgradeRequired: true,
            minimumPlanId: minimumPlanId);
}

public enum AssistantAccessReason
{
    Available,
    Disabled,
    NotConfigured,
    OrganizationRequired,
    OrganizationNotAccessible,
    UpgradeRequired
}

public sealed record AssistantAccessDecision(
    bool Enabled,
    bool HasAccess,
    bool UpgradeRequired,
    AssistantAccessReason Reason,
    string? Message,
    AssistantPlanOptions? PlanOptions,
    string? MinimumPlanId)
{
    public static AssistantAccessDecision Available(AssistantPlanOptions? planOptions) => new(true, true, false, AssistantAccessReason.Available, null, planOptions, null);

    public static AssistantAccessDecision Unavailable(
        AssistantAccessReason reason,
        string message,
        bool enabled = true,
        bool upgradeRequired = false,
        string? minimumPlanId = null) => new(enabled, false, upgradeRequired, reason, message, null, minimumPlanId);

    public AssistantAccessResponse ToResponse() => new(Enabled, HasAccess, UpgradeRequired, Message, MinimumPlanId);
}
