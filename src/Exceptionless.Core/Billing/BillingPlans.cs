using System.Collections.Generic;
using Exceptionless.Core.Models.Billing;

namespace Exceptionless.Core.Billing {
    public class BillingPlans {
        public BillingPlans(AppOptions options) {
            FreePlan = new BillingPlan {
                Id = "EX_FREE",
                Name = "Free",
                Description = "Free",
                Price = 0,
                MaxProjects = 1,
                MaxUsers = 1,
                RetentionDays = 3,
                MaxEventsPerMonth = 3000,
                HasPremiumFeatures = false
            };
    
            SmallPlan = new BillingPlan {
                Id = "EX_SMALL",
                Name = "Small",
                Description = "Small ($15/month)",
                Price = 15,
                MaxProjects = 5,
                MaxUsers = 10,
                RetentionDays = 30,
                MaxEventsPerMonth = 15000,
                HasPremiumFeatures = true
            };
    
            SmallYearlyPlan = new BillingPlan {
                Id = "EX_SMALL_YEARLY",
                Name = "Small (Yearly)",
                Description = "Small Yearly ($165/year - Save $15)",
                Price = 165,
                MaxProjects = 5,
                MaxUsers = 10,
                RetentionDays = 30,
                MaxEventsPerMonth = 15000,
                HasPremiumFeatures = true
            };
    
            MediumPlan = new BillingPlan {
                Id = "EX_MEDIUM",
                Name = "Medium",
                Description = "Medium ($49/month)",
                Price = 49,
                MaxProjects = 15,
                MaxUsers = 25,
                RetentionDays = 90,
                MaxEventsPerMonth = 75000,
                HasPremiumFeatures = true
            };
    
            MediumYearlyPlan = new BillingPlan {
                Id = "EX_MEDIUM_YEARLY",
                Name = "Medium (Yearly)",
                Description = "Medium Yearly ($539/year - Save $49)",
                Price = 539,
                MaxProjects = 15,
                MaxUsers = 25,
                RetentionDays = 90,
                MaxEventsPerMonth = 75000,
                HasPremiumFeatures = true
            };
    
            LargePlan = new BillingPlan {
                Id = "EX_LARGE",
                Name = "Large",
                Description = "Large ($99/month)",
                Price = 99,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = 180,
                MaxEventsPerMonth = 250000,
                HasPremiumFeatures = true
            };
    
            LargeYearlyPlan = new BillingPlan {
                Id = "EX_LARGE_YEARLY",
                Name = "Large (Yearly)",
                Description = "Large Yearly ($1,089/year - Save $99)",
                Price = 1089,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = 180,
                MaxEventsPerMonth = 250000,
                HasPremiumFeatures = true
            };
    
            ExtraLargePlan = new BillingPlan {
                Id = "EX_XL",
                Name = "Extra Large",
                Description = "Extra Large ($199/month)",
                Price = 199,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = 180,
                MaxEventsPerMonth = 1000000,
                HasPremiumFeatures = true
            };
    
            ExtraLargeYearlyPlan = new BillingPlan {
                Id = "EX_XL_YEARLY",
                Name = "Extra Large (Yearly)",
                Description = "Extra Large Yearly ($2,189/year - Save $199)",
                Price = 2189,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = 180,
                MaxEventsPerMonth = 1000000,
                HasPremiumFeatures = true
            };
    
            EnterprisePlan = new BillingPlan {
                Id = "EX_ENT",
                Name = "Enterprise",
                Description = "Enterprise ($499/month)",
                Price = 499,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = 180,
                MaxEventsPerMonth = 3000000,
                HasPremiumFeatures = true
            };
    
            EnterpriseYearlyPlan = new BillingPlan {
                Id = "EX_ENT_YEARLY",
                Name = "Enterprise (Yearly)",
                Description = "Enterprise Yearly ($5,489/year - Save $499)",
                Price = 5489,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = 180,
                MaxEventsPerMonth = 3000000,
                HasPremiumFeatures = true
            };
    
            UnlimitedPlan = new BillingPlan {
                Id = "EX_UNLIMITED",
                Name = "Unlimited",
                Description = "Unlimited",
                IsHidden = true,
                Price = 0,
                MaxProjects = -1,
                MaxUsers = -1,
                RetentionDays = options.MaximumRetentionDays,
                MaxEventsPerMonth = -1,
                HasPremiumFeatures = true
            };
            
            Plans = new List<BillingPlan> { FreePlan, SmallYearlyPlan, MediumYearlyPlan, LargeYearlyPlan, ExtraLargeYearlyPlan, EnterpriseYearlyPlan, SmallPlan, MediumPlan, LargePlan, ExtraLargePlan, EnterprisePlan, UnlimitedPlan };
        }

        public BillingPlan FreePlan  { get; }

        public BillingPlan SmallPlan  { get; }

        public BillingPlan SmallYearlyPlan  { get; }

        public BillingPlan MediumPlan  { get; }

        public BillingPlan MediumYearlyPlan  { get; }

        public BillingPlan LargePlan  { get; }

        public BillingPlan LargeYearlyPlan  { get; }

        public BillingPlan ExtraLargePlan  { get; }

        public BillingPlan ExtraLargeYearlyPlan  { get; }

        public BillingPlan EnterprisePlan  { get; }

        public BillingPlan EnterpriseYearlyPlan  { get; }

        public BillingPlan UnlimitedPlan { get; }

        public List<BillingPlan> Plans { get; }
    }
}