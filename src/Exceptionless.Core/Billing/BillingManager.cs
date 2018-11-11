using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Billing {
    public class BillingManager {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;

        public BillingManager(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, IOptions<AppOptions> options) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;

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
                RetentionDays = options.Value.MaximumRetentionDays,
                MaxEventsPerMonth = -1,
                HasPremiumFeatures = true
            };
            
            Plans = new List<BillingPlan> { FreePlan, SmallYearlyPlan, MediumYearlyPlan, LargeYearlyPlan, ExtraLargeYearlyPlan, EnterpriseYearlyPlan, SmallPlan, MediumPlan, LargePlan, ExtraLargePlan, EnterprisePlan, UnlimitedPlan };
        }

        public async Task<bool> CanAddOrganizationAsync(User user) {
            if (user == null)
                return false;

            var organizations = (await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray()).AnyContext()).Where(o => o.PlanId == FreePlan.Id);
            return !organizations.Any();
        }

        public async Task<bool> CanAddUserAsync(Organization organization) {
            if (String.IsNullOrWhiteSpace(organization?.Id))
                return false;

            long numberOfUsers = (await _userRepository.GetByOrganizationIdAsync(organization.Id).AnyContext()).Total + organization.Invites.Count;
            return organization.MaxUsers <= -1 || numberOfUsers < organization.MaxUsers;
        }

        public async Task<bool> CanAddProjectAsync(Project project) {
            if (String.IsNullOrWhiteSpace(project?.OrganizationId))
                return false;

            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId).AnyContext();
            if (organization == null)
                return false;

            long projectCount = await _projectRepository.GetCountByOrganizationIdAsync(project.OrganizationId).AnyContext();
            return organization.MaxProjects == -1 || projectCount < organization.MaxProjects;
        }

        public async Task<bool> HasPremiumFeaturesAsync(string organizationId) {
            var organization = await _organizationRepository.GetByIdAsync(organizationId).AnyContext();
            if (organization == null)
                return false;

            return organization.HasPremiumFeatures;
        }

        public async Task<ChangePlanResult> CanDownGradeAsync(Organization organization, BillingPlan plan, User user) {
            if (String.IsNullOrWhiteSpace(organization?.Id))
                return ChangePlanResult.FailWithMessage("Invalid Organization");

            long currentNumberOfUsers = (await _userRepository.GetByOrganizationIdAsync(organization.Id).AnyContext()).Total + organization.Invites.Count;
            int maxUsers = plan.MaxUsers != -1 ? plan.MaxUsers : Int32.MaxValue;
            if (currentNumberOfUsers > maxUsers)
                return ChangePlanResult.FailWithMessage($"Please remove {currentNumberOfUsers - maxUsers} user{((currentNumberOfUsers - maxUsers) > 0 ? "s" : String.Empty)} and try again.");

            int maxProjects = plan.MaxProjects != -1 ? plan.MaxProjects : Int32.MaxValue;
            long projectCount = await _projectRepository.GetCountByOrganizationIdAsync(organization.Id).AnyContext();
            if (projectCount > maxProjects)
                return ChangePlanResult.FailWithMessage($"Please remove {projectCount - maxProjects} project{((projectCount - maxProjects) > 0 ? "s" : String.Empty)} and try again.");

            // Ensure the user can't be apart of more than one free plan.
            if (String.Equals(plan.Id, FreePlan.Id) && user != null && (await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray()).AnyContext()).Any(o => String.Equals(o.PlanId, FreePlan.Id)))
                return ChangePlanResult.FailWithMessage("You already have one free account. You are not allowed to create more than one free account.");

            return new ChangePlanResult { Success = true };
        }

        public BillingPlan GetBillingPlan(string planId) {
            return Plans.FirstOrDefault(p => String.Equals(p.Id, planId, StringComparison.OrdinalIgnoreCase));
        }

        public void ApplyBillingPlan(Organization organization, BillingPlan plan, User user = null, bool updateBillingPrice = true) {
            organization.PlanId = plan.Id;
            organization.PlanName = plan.Name;
            organization.PlanDescription = plan.Description;
            organization.BillingChangeDate = SystemClock.UtcNow;

            if (updateBillingPrice)
                organization.BillingPrice = plan.Price;

            if (user != null)
                organization.BillingChangedByUserId = user.Id;

            organization.MaxUsers = plan.MaxUsers;
            organization.MaxProjects = plan.MaxProjects;
            organization.RetentionDays = plan.RetentionDays;
            organization.MaxEventsPerMonth = plan.MaxEventsPerMonth;
            organization.HasPremiumFeatures = plan.HasPremiumFeatures;

            organization.SetMonthlyUsage(organization.GetCurrentMonthlyTotal(), organization.GetCurrentMonthlyBlocked(), organization.GetCurrentMonthlyTooBig());
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