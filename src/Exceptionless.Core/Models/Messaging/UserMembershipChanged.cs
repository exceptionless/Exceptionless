using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Messaging.Models;

public record UserMembershipChanged
{
    public required ChangeType ChangeType { get; init; }
    public required string UserId { get; init; }
    public required string OrganizationId { get; init; }
}
