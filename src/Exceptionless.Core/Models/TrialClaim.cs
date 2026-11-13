using System.Text.Json.Serialization;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

/// <summary>
/// Records a mailbox's latest no-card trial. It's kept apart from the user so deleting the account and
/// signing up again doesn't make the mailbox eligible again. Kept for
/// <see cref="Exceptionless.Core.Configuration.BillingOptions.TrialClaimRetentionDays"/> (12 months by default), including
/// after user/organization deletion, then removed so the mailbox can trial again. Contains no email, user ID, or organization ID.
/// </summary>
public sealed class TrialClaim : IIdentity, IHaveCreatedDate, IVersioned
{
    /// <summary>
    /// SHA-256 identifier from <see cref="Exceptionless.Core.Billing.TrialEmailAddress.GetClaimId"/>.
    /// </summary>
    public string Id { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    // Repository concurrency metadata is not part of the retained claim.
    [JsonIgnore]
    public string Version { get; set; } = String.Empty;
}
