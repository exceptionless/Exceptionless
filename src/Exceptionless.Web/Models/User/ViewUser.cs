using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public record ViewUser : IIdentity
{
    public string Id { get; set; } = null!;
    public ISet<string> OrganizationIds { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string EmailAddress { get; init; } = null!;
    public bool EmailNotificationsEnabled { get; init; }
    public bool IsEmailAddressVerified { get; init; }
    public bool IsActive { get; init; }
    public bool IsInvite { get; init; }
    public ISet<string> Roles { get; init; } = null!;
}
