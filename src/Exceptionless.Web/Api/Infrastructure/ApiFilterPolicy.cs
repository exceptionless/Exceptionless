using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;

namespace Exceptionless.Web.Api.Infrastructure;

public static class ApiFilterPolicy
{
    public const string PremiumSearchUpgradeMessage = "Please upgrade your plan to use premium search features.";
    public const string SuspendedOrPremiumSearchUpgradeDescription = "The organization is suspended, or you must upgrade your plan to use premium search features.";

    public static bool IsPremiumFeatureQueryBlocked(AppFilter filter)
    {
        return filter.UsesPremiumFeatures
            && filter.Organizations.Count > 0
            && filter.Organizations.All(organization => !organization.HasPremiumFeatures);
    }

    public static bool ShouldApplySystemFilter(AppFilter filter, string? userFilter, HttpRequest? request = null)
    {
        if (request is null || !request.IsGlobalAdmin())
            return true;

        if (!filter.IsUserOrganizationsFilter || String.IsNullOrEmpty(userFilter))
            return true;

        // Explicitly scoped all-organization searches are the existing support/impersonation path.
        var scope = GetFilterScopeVisitor.Run(userFilter);
        return !scope.HasScope;
    }
}
