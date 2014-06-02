#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Exceptionless.Models {
    public class Organization : IIdentity {
        public Organization() {
            Invites = new Collection<Invite>();
            BillingStatus = BillingStatus.Trialing;
            OverageHours = new Collection<UsageInfo>();
            Usage = new Collection<UsageInfo>();
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
        /// Billing plan that the organization belongs to.
        /// </summary>
        public string PlanId { get; set; }

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
        /// Maximum number of error occurrences allowed per month.
        /// </summary>
        public int MaxErrorsPerMonth { get; set; }

        /// <summary>
        /// Number of days stats data is retained.
        /// </summary>
        public int RetentionDays { get; set; }

        /// <summary>
        /// If true, the account is suspended and can't be used.
        /// </summary>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// The code indicating why the account was suspended.
        /// </summary>
        public string SuspensionCode { get; set; }

        /// <summary>
        /// Any notes on why the account was suspended.
        /// </summary>
        public string SuspensionNotes { get; set; }

        /// <summary>
        /// The reason the account was suspended.
        /// </summary>
        public DateTime? SuspensionDate { get; set; }

        /// <summary>
        /// User id that the suspended the account.
        /// </summary>
        public string SuspendedByUserId { get; set; }

        /// <summary>
        /// If true, premium features will be enabled.
        /// </summary>
        public bool HasPremiumFeatures { get; set; }

        /// <summary>
        /// Maximum number of users allowed by the current plan.
        /// </summary>
        public long MaxUsers { get; set; }

        /// <summary>
        /// Maximum number of projects allowed by the current plan.
        /// </summary>
        public int MaxProjects { get; set; }

        /// <summary>
        /// Total number of projects.
        /// </summary>
        public int ProjectCount { get; set; }

        /// <summary>
        /// Current number of error stacks in the system.
        /// </summary>
        public long StackCount { get; set; }

        /// <summary>
        /// Current number of error occurrences in the system.
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// The date that the latest error occurred.
        /// </summary>
        public DateTime LastErrorDate { get; set; }

        /// <summary>
        /// Total errors logged by our system.
        /// </summary>
        public long TotalErrorCount { get; set; }

        /// <summary>
        /// Organization invites.
        /// </summary>
        public ICollection<Invite> Invites { get; set; }

        /// <summary>
        /// Hours over error limit.
        /// </summary>
        public ICollection<UsageInfo> OverageHours { get; set; }

        /// <summary>
        /// Account error usage information.
        /// </summary>
        public ICollection<UsageInfo> Usage { get; set; }
    }

    public class UsageInfo {
        public DateTime Date { get; set; }
        public int Total { get; set; }
        public int Accepted { get; set; }
        public int Limit { get; set; }
    }

    public enum BillingStatus {
        Trialing = 0,
        Active = 1,
        PastDue = 2,
        Canceled = 3,
        Unpaid = 4
    }
}