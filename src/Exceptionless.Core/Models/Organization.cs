using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Billing;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("{Id}, {Name}, {PlanName}")]
public class Organization : IData, IOwnedByOrganizationWithIdentity, IHaveDates, ISupportSoftDeletes, IValidatableObject
{
    public Organization()
    {
        Invites = new Collection<Invite>();
        BillingStatus = BillingStatus.Trialing;
        Usage = new SortedSet<UsageInfo>(Comparer<UsageInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
        UsageHours = new SortedSet<UsageHourInfo>(Comparer<UsageHourInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
        Data = new DataDictionary();
    }

    /// <summary>
    /// Unique id that identifies the organization.
    /// </summary>
    [Required]
    [ObjectId]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Name of the organization.
    /// </summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Stripe customer id that will be charged.
    /// </summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// Billing plan id that the organization belongs to.
    /// </summary>
    [Required]
    public string PlanId { get; set; } = null!;

    /// <summary>
    /// Billing plan name that the organization belongs to.
    /// </summary>
    public string PlanName { get; set; } = null!;

    /// <summary>
    /// Billing plan description that the organization belongs to.
    /// </summary>
    public string PlanDescription { get; set; } = null!;

    /// <summary>
    /// Last 4 digits of the credit card used for billing.
    /// </summary>
    public string? CardLast4 { get; set; }

    /// <summary>
    /// Date the organization first subscribed to a paid plan.
    /// </summary>
    public DateTime? SubscribeDate { get; set; }

    /// <summary>
    /// Date the billing information was last changed.
    /// </summary>
    public DateTime BillingChangeDate { get; set; }

    /// <summary>
    /// User id that the billing information was last changed by.
    /// </summary>
    [ObjectId]
    public string? BillingChangedByUserId { get; set; }

    /// <summary>
    /// Organization's current billing status.
    /// </summary>
    public BillingStatus BillingStatus { get; set; }

    /// <summary>
    /// The price of the plan that this organization is currently on.
    /// </summary>
    public decimal BillingPrice { get; set; }

    /// <summary>
    /// Maximum number of event occurrences allowed per month.
    /// </summary>
    public int MaxEventsPerMonth { get; set; }

    /// <summary>
    /// Bonus number of event occurrences allowed per month.
    /// </summary>
    public int BonusEventsPerMonth { get; set; }

    /// <summary>
    /// Date that the bonus events expire.
    /// </summary>
    public DateTime? BonusExpiration { get; set; }

    /// <summary>
    /// Number of days event data is retained.
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// If true, the account is suspended and can't be used.
    /// </summary>
    public bool IsSuspended { get; set; }

    /// <summary>
    /// The code indicating why the account was suspended.
    /// </summary>
    public SuspensionCode? SuspensionCode { get; set; }

    /// <summary>
    /// Any notes on why the account was suspended.
    /// </summary>
    public string? SuspensionNotes { get; set; }

    /// <summary>
    /// The reason the account was suspended.
    /// </summary>
    public DateTime? SuspensionDate { get; set; }

    /// <summary>
    /// User id that suspended the account.
    /// </summary>
    [ObjectId]
    public string? SuspendedByUserId { get; set; }

    /// <summary>
    /// If true, premium features will be enabled.
    /// </summary>
    public bool HasPremiumFeatures { get; set; }

    /// <summary>
    /// Set of enabled feature flags for this organization (e.g., "feature-saved-views").
    /// Feature identifiers are always stored in lowercase.
    /// </summary>
    public ISet<string> Features { get; set; } = new HashSet<string>();

    /// <summary>
    /// Maximum number of users allowed by the current plan.
    /// </summary>
    public int MaxUsers { get; set; }

    /// <summary>
    /// Maximum number of projects allowed by the current plan.
    /// </summary>
    public int MaxProjects { get; set; }

    /// <summary>
    /// Organization invites.
    /// </summary>
    public ICollection<Invite> Invites { get; set; }

    /// <summary>
    /// Hourly account event usage information.
    /// </summary>
    public ICollection<UsageHourInfo> UsageHours { get; set; }

    /// <summary>
    /// Account event usage information.
    /// </summary>
    public ICollection<UsageInfo> Usage { get; set; }
    public DateTime? LastEventDateUtc { get; set; }

    /// <summary>
    /// Optional data entries that contain additional configuration information for this organization.
    /// </summary>
    public DataDictionary? Data { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }

    string IOwnedByOrganization.OrganizationId { get { return Id; } set { Id = value; } }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var plans = validationContext.GetService(typeof(BillingPlans)) as BillingPlans;
        if (plans is not null && PlanId == plans.FreePlan.Id && HasPremiumFeatures)
        {
            yield return new ValidationResult("Premium features cannot be enabled on the free plan.",
                [nameof(HasPremiumFeatures)]);
        }

        if (BillingPrice > 0)
        {
            if (String.IsNullOrEmpty(StripeCustomerId))
            {
                yield return new ValidationResult("The stripe customer should be set on paid plans.",
                    [nameof(StripeCustomerId)]);
            }

            if (String.IsNullOrEmpty(CardLast4))
            {
                yield return new ValidationResult("The card last four should be set on paid plans.",
                    [nameof(CardLast4)]);
            }

            if (SubscribeDate is null || SubscribeDate == DateTime.MinValue)
            {
                yield return new ValidationResult("The subscribe date should be set on paid plans.",
                    [nameof(SubscribeDate)]);
            }

            if (BillingChangeDate == DateTime.MinValue)
            {
                yield return new ValidationResult("The billing change date should be set on paid plans.",
                    [nameof(BillingChangeDate)]);
            }

            if (String.IsNullOrEmpty(BillingChangedByUserId) || BillingChangedByUserId.Length != 24)
            {
                yield return new ValidationResult("The billing changed by user id should be set on paid plans.",
                    [nameof(BillingChangedByUserId)]);
            }
        }

        if (IsSuspended)
        {
            if (SuspensionCode is null)
            {
                yield return new ValidationResult("Please specify a valid suspension code.",
                    [nameof(SuspensionCode)]);
            }

            if (SuspensionDate is null || SuspensionDate == DateTime.MinValue)
            {
                yield return new ValidationResult("Please specify a valid suspension date.",
                    [nameof(SuspensionDate)]);
            }

            if (String.IsNullOrEmpty(SuspendedByUserId))
            {
                yield return new ValidationResult("Please specify a user id of user that suspended this organization.",
                    [nameof(SuspendedByUserId)]);
            }

            if (SuspensionCode is Models.SuspensionCode.Other && String.IsNullOrEmpty(SuspensionNotes))
            {
                yield return new ValidationResult("Please specify a suspension note.",
                    [nameof(SuspensionNotes)]);
            }
        }
        else
        {
            if (SuspensionCode is not null)
            {
                yield return new ValidationResult("The suspension code cannot be set while an organization is not suspended.",
                    [nameof(SuspensionCode)]);
            }

            if (SuspensionDate is not null && SuspensionDate != DateTime.MinValue)
            {
                yield return new ValidationResult("The suspension date cannot be set while an organization is not suspended.",
                    [nameof(SuspensionDate)]);
            }

            if (SuspendedByUserId is not null)
            {
                yield return new ValidationResult("The suspended by user id cannot be set while an organization is not suspended.",
                    [nameof(SuspendedByUserId)]);
            }
        }
    }
}

public enum BillingStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3,
    Unpaid = 4
}

/// <summary>
/// Well-known organization feature flag identifiers.
/// </summary>
public static class OrganizationFeatures
{
    /// <summary>Enables the Saved Views feature for the organization.</summary>
    public const string SavedViews = "feature-saved-views";
}
