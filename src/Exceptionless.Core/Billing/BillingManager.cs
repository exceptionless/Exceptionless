using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Utility;

namespace Exceptionless.Core.Billing {
    public class BillingManager {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;

        public BillingManager(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
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
            int maxUsers = plan.MaxUsers != -1 ? plan.MaxUsers : int.MaxValue;
            if (currentNumberOfUsers > maxUsers)
                return ChangePlanResult.FailWithMessage($"Please remove {currentNumberOfUsers - maxUsers} user{((currentNumberOfUsers - maxUsers) > 0 ? "s" : String.Empty)} and try again.");

            int maxProjects = plan.MaxProjects != -1 ? plan.MaxProjects : int.MaxValue;
            long projectCount = await _projectRepository.GetCountByOrganizationIdAsync(organization.Id).AnyContext();
            if (projectCount > maxProjects)
                return ChangePlanResult.FailWithMessage($"Please remove {projectCount - maxProjects} project{((projectCount - maxProjects) > 0 ? "s" : String.Empty)} and try again.");

            // Ensure the user can't be apart of more than one free plan.
            if (String.Equals(plan.Id, FreePlan.Id) && user != null && (await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray()).AnyContext()).Any(o => String.Equals(o.PlanId, FreePlan.Id)))
                return ChangePlanResult.FailWithMessage("You already have one free account. You are not allowed to create more than one free account.");

            return new ChangePlanResult { Success = true };
        }

        public static BillingPlan GetBillingPlan(string planId) {
            return Plans.FirstOrDefault(p => String.Equals(p.Id, planId, StringComparison.OrdinalIgnoreCase));
        }

        public static BillingPlan GetBillingPlanByUpsellingRetentionPeriod(int retentionDays) {
            return Plans.Where(p => p.RetentionDays > retentionDays && p.Price > 0).OrderBy(p => p.RetentionDays).ThenBy(p => p.Price).FirstOrDefault();
        }

        public static void ApplyBillingPlan(Organization organization, BillingPlan plan, User user = null, bool updateBillingPrice = true) {
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

        public static BillingPlan FreePlan => new BillingPlan {
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

        public static BillingPlan SmallPlan => new BillingPlan {
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

        public static BillingPlan SmallYearlyPlan => new BillingPlan {
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

        public static BillingPlan MediumPlan => new BillingPlan {
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

        public static BillingPlan MediumYearlyPlan => new BillingPlan {
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

        public static BillingPlan LargePlan => new BillingPlan {
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

        public static BillingPlan LargeYearlyPlan => new BillingPlan {
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

        public static BillingPlan ExtraLargePlan => new BillingPlan {
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

        public static BillingPlan ExtraLargeYearlyPlan => new BillingPlan {
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

        public static BillingPlan EnterprisePlan => new BillingPlan {
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

        public static BillingPlan EnterpriseYearlyPlan => new BillingPlan {
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

        public static BillingPlan UnlimitedPlan => new BillingPlan {
            Id = "EX_UNLIMITED",
            Name = "Unlimited",
            Description = "Unlimited",
            IsHidden = true,
            Price = 0,
            MaxProjects = -1,
            MaxUsers = -1,
            RetentionDays = Settings.Current.MaximumRetentionDays,
            MaxEventsPerMonth = -1,
            HasPremiumFeatures = true
        };

        public static readonly BillingPlan[] Plans = { FreePlan, SmallYearlyPlan, MediumYearlyPlan, LargeYearlyPlan, ExtraLargeYearlyPlan, EnterpriseYearlyPlan, SmallPlan, MediumPlan, LargePlan, ExtraLargePlan, EnterprisePlan, UnlimitedPlan };
    }
}