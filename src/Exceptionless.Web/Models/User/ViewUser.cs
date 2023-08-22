using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public record ViewUser : IIdentity
{
    public string Id { get; set; } = null!;
    public ICollection<string> OrganizationIds { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public bool EmailNotificationsEnabled { get; set; }
    public bool IsEmailAddressVerified { get; set; }
    public bool IsActive { get; set; }
    public bool IsInvite { get; set; }
    public ICollection<string> Roles { get; set; } = null!;
}
