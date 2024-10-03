using System.Collections.ObjectModel;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public record User : IIdentity, IHaveDates
{
    /// <summary>
    /// Unique id that identifies an user.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The organizations that the user has access to.
    /// </summary>
    public ICollection<string> OrganizationIds { get; } = new Collection<string>();

    public string? Password { get; set; }
    public string? Salt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime PasswordResetTokenExpiration { get; set; }
    public ICollection<OAuthAccount> OAuthAccounts { get; } = new Collection<OAuthAccount>();

    /// <summary>
    /// Gets or sets the users Full Name.
    /// </summary>
    public string FullName { get; set; } = null!;

    public string EmailAddress { get; set; } = null!;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool IsEmailAddressVerified { get; set; }
    public string? VerifyEmailAddressToken { get; set; }
    public DateTime VerifyEmailAddressTokenExpiration { get; set; }

    /// <summary>
    /// Gets or sets the users active state.
    /// </summary>
    public bool IsActive { get; init; } = true;

    public ICollection<string> Roles { get; init; } = new Collection<string>();

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
