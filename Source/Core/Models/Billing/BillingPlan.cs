using System;
using System.Diagnostics;

namespace Exceptionless.Core.Models.Billing {
    [DebuggerDisplay("Id: {Id} Name: {Name} Price: {Price}")]
    public class BillingPlan {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int MaxProjects { get; set; }
        public int MaxUsers { get; set; }
        public int RetentionDays { get; set; }
        public int MaxEventsPerMonth { get; set; }
        public bool HasPremiumFeatures { get; set; }
        public bool IsHidden { get; set; }
    }
}