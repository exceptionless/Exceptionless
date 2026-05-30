using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Web.Hubs;

internal static class PushDisconnectCleanup
{
    public static async Task<HashSet<string>> GetOrganizationIdsAsync(ClaimsPrincipal user, string connectionId, IConnectionMapping connectionMapping, Func<Task<User?>> getCurrentUserAsync, ILogger logger)
    {
        var organizationIds = new HashSet<string>(await connectionMapping.GetConnectionGroupsAsync(connectionId).ConfigureAwait(false));
        organizationIds.UnionWith(user.GetOrganizationIds());
        string? userId = user.GetUserId();
        if (String.IsNullOrEmpty(userId))
            return organizationIds;

        try
        {
            var currentUser = await getCurrentUserAsync().ConfigureAwait(false);
            if (currentUser is not null)
                organizationIds.UnionWith(currentUser.OrganizationIds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falling back to tracked push disconnect cleanup for user {UserId}", userId);
        }

        return organizationIds;
    }
}
