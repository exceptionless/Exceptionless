using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Web.Hubs;

internal sealed record PushPrincipal(string ConnectionOwnerId, string? UserId, string? TokenId, IReadOnlyCollection<string> OrganizationIds, bool FollowMembershipAdditions)
{
    public static bool TryCreate(ClaimsPrincipal principal, [NotNullWhen(true)] out PushPrincipal? pushPrincipal)
    {
        string? connectionOwnerId = principal.IsAuthenticated()
            ? principal.GetClaimValue(ClaimTypes.NameIdentifier)
            : null;
        if (String.IsNullOrEmpty(connectionOwnerId))
        {
            pushPrincipal = null;
            return false;
        }

        string? tokenId = principal.GetClaimValue(IdentityUtils.LoggedInUsersTokenId);
        string? userId = principal.GetUserId() ?? (tokenId is not null ? connectionOwnerId : null);
        if (tokenId is null && principal.IsTokenAuthType())
            tokenId = connectionOwnerId;

        pushPrincipal = new PushPrincipal(
            connectionOwnerId,
            userId,
            tokenId,
            principal.GetOrganizationIds(),
            principal.IsUserAuthType());
        return true;
    }
}
