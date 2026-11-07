using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

/// <summary>
/// Records that a mailbox started its one no-card trial. It's kept apart from the user so deleting the account and
/// signing up again doesn't make the mailbox eligible again, and it stores a hash rather than the address.
/// </summary>
public sealed class TrialClaim : IIdentity, IHaveCreatedDate
{
    /// <summary>
    /// SHA-256 of the normalized email address, from <see cref="Exceptionless.Core.Billing.TrialEmailAddress.GetClaimId"/>.
    /// </summary>
    public string Id { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
}
