using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;

namespace Exceptionless.Api.Models {
    public class ViewOrganization : IIdentity, IData, IHaveCreatedDate {
        public string Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Name { get; set; }
        public string PlanId { get; set; }
        public string PlanName { get; set; }
        public string PlanDescription { get; set; }
        public string CardLast4 { get; set; }
        public DateTime? SubscribeDate { get; set; }
        public DateTime? BillingChangeDate { get; set; }
        public string BillingChangedByUserId { get; set; }
        public BillingStatus BillingStatus { get; set; }
        public decimal BillingPrice { get; set; }
        public int MaxEventsPerMonth { get; set; }
        public int BonusEventsPerMonth { get; set; }
        public DateTime? BonusExpiration { get; set; }
        public int RetentionDays { get; set; }
        public bool IsSuspended { get; set; }
        public string SuspensionCode { get; set; }
        public string SuspensionNotes { get; set; }
        public DateTime? SuspensionDate { get; set; }
        public bool HasPremiumFeatures { get; set; }
        public int MaxUsers { get; set; }
        public int MaxProjects { get; set; }
        public long ProjectCount { get; set; }
        public long StackCount { get; set; }
        public long EventCount { get; set; }
        public ICollection<Invite> Invites { get; set; }
        public ICollection<UsageInfo> OverageHours { get; set; }
        public ICollection<UsageInfo> Usage { get; set; }
        public DataDictionary Data { get; set; }

        public bool IsOverHourlyLimit { get; set; }
        public bool IsOverMonthlyLimit { get; set; }
        public bool IsOverRequestLimit { get; set; }
    }
}