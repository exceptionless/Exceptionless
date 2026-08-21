using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Web.Hubs;

internal sealed record PushPrincipal(string ConnectionOwnerId, string? UserId, string? TokenId, IReadOnlyCollection<string> OrganizationIds)
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
        if (tokenId is null && principal.IsTokenAuthType())
            tokenId = connectionOwnerId;

        pushPrincipal = new PushPrincipal(
            connectionOwnerId,
            principal.GetUserId(),
            tokenId,
            principal.GetOrganizationIds());
        return true;
    }
}
