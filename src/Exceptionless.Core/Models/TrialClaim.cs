using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

/// <summary>
/// Records that a mailbox started its one no-card trial. It's kept apart from the user so deleting the account and
/// signing up again doesn't make the mailbox eligible again. Retained for the lifetime of the one-trial-per-mailbox
/// program, including after user/organization deletion. Contains no email, user ID, or organization ID.
/// </summary>
public sealed class TrialClaim : IIdentity, IHaveCreatedDate
{
    /// <summary>
    /// Versioned HMAC identifier from <see cref="Exceptionless.Core.Billing.TrialEmailAddress.GetClaimId"/>.
    /// </summary>
    public string Id { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
}
