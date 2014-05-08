using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class ViewOrganization : IIdentity {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PlanId { get; set; }
        public string CardLast4 { get; set; }
        public DateTime? SubscribeDate { get; set; }
        public DateTime? BillingChangeDate { get; set; }
        public string BillingChangedByUserId { get; set; }
        public BillingStatus BillingStatus { get; set; }
        public decimal BillingPrice { get; set; }
        public int MaxErrorsPerDay { get; set; }
        public int RetentionDays { get; set; }
        public bool IsSuspended { get; set; }
        public string SuspensionCode { get; set; }
        public string SuspensionNotes { get; set; }
        public DateTime? SuspensionDate { get; set; }
        public bool HasPremiumFeatures { get; set; }
        public long MaxUsers { get; set; }
        public int MaxProjects { get; set; }
        public int ProjectCount { get; set; }
        public long StackCount { get; set; }
        public long EventCount { get; set; }
        public DateTime LastEventDate { get; set; }
        public long TotalEventCount { get; set; }
        public ICollection<Invite> Invites { get; set; }
        public ICollection<OverageInfo> OverageDays { get; set; }
        public DataDictionary Data { get; set; }
    }
}