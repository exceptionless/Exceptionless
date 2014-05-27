using System;
using Exceptionless.Models;

namespace Exceptionless.App.Models.Organization {
    public class OrganizationInfoModel {
        public string Id { get; set; }
        public string Name { get; set; }
        public string StripeCustomerId { get; set; }
        public string PlanId { get; set; }
        public string CardLast4 { get; set; }
        public DateTime? SubscribeDate { get; set; }
        public DateTime? BillingChangeDate { get; set; }
        public string BillingChangedByUserId { get; set; }
        public BillingStatus BillingStatus { get; set; }
        public decimal BillingPrice { get; set; }
        public bool IsSuspended { get; set; }
        public string SuspensionCode { get; set; }
        public string SuspensionNotes { get; set; }
        public DateTime? SuspensionDate { get; set; }
        public string SuspendedByUserId { get; set; }
        public int ProjectCount { get; set; }
        public long StackCount { get; set; }
        public long ErrorCount { get; set; }
        public DateTime LastErrorDate { get; set; }
        public long TotalErrorCount { get; set; }

        public bool IsOverHourlyLimit { get; set; }
        public bool IsOverMonthlyLimit { get; set; }
    }
}