using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    [DebuggerDisplay("{Id}, {Name}, {PlanName}")]
    public class Organization : IData, IOwnedByOrganizationWithIdentity, IHaveDates {
        public Organization() {
            Invites = new Collection<Invite>();
            BillingStatus = BillingStatus.Trialing;
            Usage = new Collection<UsageInfo>();
            OverageHours = new Collection<UsageInfo>();
            Data = new DataDictionary();
        }

        /// <summary>
        /// Unique id that identifies the organization.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the organization.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Stripe customer id that will be charged.
        /// </summary>
        public string StripeCustomerId { get; set; }

        /// <summary>
        /// Billing plan id that the organization belongs to.
        /// </summary>
        public string PlanId { get; set; }

        /// <summary>
        /// Billing plan name that the organization belongs to.
        /// </summary>
        public string PlanName { get; set; }

        /// <summary>
        /// Billing plan description that the organization belongs to.
        /// </summary>
        public string PlanDescription { get; set; }

        /// <summary>
        /// Last 4 digits of the credit card used for billing.
        /// </summary>
        public string CardLast4 { get; set; }

        /// <summary>
        /// Date the organization first subscribed to a paid plan.
        /// </summary>
        public DateTime? SubscribeDate { get; set; }

        /// <summary>
        /// Date the billing information was last changed.
        /// </summary>
        public DateTime? BillingChangeDate { get; set; }

        /// <summary>
        /// User id that the billing information was last changed by.
        /// </summary>
        public string BillingChangedByUserId { get; set; }

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
        public string SuspensionNotes { get; set; }

        /// <summary>
        /// The reason the account was suspended.
        /// </summary>
        public DateTime? SuspensionDate { get; set; }

        /// <summary>
        /// User id that suspended the account.
        /// </summary>
        public string SuspendedByUserId { get; set; }

        /// <summary>
        /// If true, premium features will be enabled.
        /// </summary>
        public bool HasPremiumFeatures { get; set; }

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
        /// Hours over event limit.
        /// </summary>
        public ICollection<UsageInfo> OverageHours { get; set; }

        /// <summary>
        /// Account event usage information.
        /// </summary>
        public ICollection<UsageInfo> Usage { get; set; }

        /// <summary>
        /// Optional data entries that contain additional configuration information for this organization.
        /// </summary>
        public DataDictionary Data { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        string IOwnedByOrganization.OrganizationId { get { return Id; } set { Id = value; } }
    }

    public enum BillingStatus {
        Trialing = 0,
        Active = 1,
        PastDue = 2,
        Canceled = 3,
        Unpaid = 4
    }
}
