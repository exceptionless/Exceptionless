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
using System.Linq;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

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

        public bool CanAddOrganization(User user) {
            if (user == null)
                return false;

            var organizations = _organizationRepository.GetByIds(user.OrganizationIds).Where(o => o.PlanId == FreePlan.Id);
            return !organizations.Any();
        }

        public bool CanAddUser(Organization organization) {
            if (organization == null || String.IsNullOrWhiteSpace(organization.Id))
                return false;

            int numberOfUsers = _userRepository.GetByOrganizationId(organization.Id).Count + organization.Invites.Count;
            return organization.MaxUsers <= -1 || numberOfUsers < organization.MaxUsers;
        }

        public bool CanAddProject(Project project) {
            if (project == null || String.IsNullOrWhiteSpace(project.OrganizationId))
                return false;

            var organization = _organizationRepository.GetById(project.OrganizationId);
            if (organization == null)
                return false;

            long projectCount = _projectRepository.GetCountByOrganizationId(project.OrganizationId);
            return organization.MaxProjects == -1 || projectCount < organization.MaxProjects;
        }

        public bool CanAddIntegration(Project project) {
            if (project == null || String.IsNullOrWhiteSpace(project.OrganizationId))
                return false;

            return HasPremiumFeatures(project.OrganizationId);
        }

        public bool HasPremiumFeatures(string organizationId) {
            var organization = _organizationRepository.GetById(organizationId);
            if (organization == null)
                return false;

            return organization.HasPremiumFeatures;
        }

        public bool CanDownGrade(Organization organization, BillingPlan plan, User user, out string message) {
            if (organization == null || String.IsNullOrWhiteSpace(organization.Id)) {
                message = "Invalid Organization";
                return false;
            }

            int currentNumberOfUsers = _userRepository.GetByOrganizationId(organization.Id).Count() + organization.Invites.Count;
            int maxUsers = plan.MaxUsers != -1 ? plan.MaxUsers : int.MaxValue;
            if (currentNumberOfUsers > maxUsers) {
                message = String.Format("Please remove {0} user{1} and try again.", currentNumberOfUsers - maxUsers, (currentNumberOfUsers - maxUsers) > 0 ? "s" : String.Empty);
                return false;
            }

            int maxProjects = plan.MaxProjects != -1 ? plan.MaxProjects : int.MaxValue;
            long projectCount = _projectRepository.GetCountByOrganizationId(organization.Id);
            if (projectCount > maxProjects) {
                message = String.Format("Please remove {0} project{1} and try again.", projectCount - maxProjects, (projectCount - maxProjects) > 0 ? "s" : String.Empty);
                return false;
            }

            // Ensure the user can't be apart of more than one free plan.
            if (String.Equals(plan.Id, FreePlan.Id) && user != null && _organizationRepository.GetByIds(user.OrganizationIds).Any(o => String.Equals(o.PlanId, FreePlan.Id))) {
                message = "You already have one free account. You are not allowed to create more than one free account.";
                return false;
            }

            message = String.Empty;
            return true;
        }

        public BillingPlan GetBillingPlan(string planId) {
            return Plans.FirstOrDefault(p => String.Equals(p.Id, planId, StringComparison.OrdinalIgnoreCase));
        }

        public void ApplyBillingPlan(Organization organization, BillingPlan plan, User user, bool updateBillingPrice = true) {
            organization.PlanId = plan.Id;
            organization.BillingChangeDate = DateTime.Now;

            if (updateBillingPrice)
                organization.BillingPrice = plan.Price;

            organization.BillingChangedByUserId = user.Id;
            organization.MaxUsers = plan.MaxUsers;
            organization.MaxProjects = plan.MaxProjects;
            organization.RetentionDays = plan.RetentionDays;
            organization.MaxEventsPerMonth = plan.MaxEventsPerMonth;
            organization.HasPremiumFeatures = plan.HasPremiumFeatures;
        }

        public static BillingPlan FreePlan {
            get {
                return new BillingPlan {
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
            }
        }

        public static BillingPlan SmallPlan {
            get {
                return new BillingPlan {
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
            }
        }

        public static BillingPlan SmallYearlyPlan {
            get {
                return new BillingPlan {
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
            }
        }

        public static BillingPlan MediumPlan {
            get {
                return new BillingPlan {
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
            }
        }

        public static BillingPlan MediumYearlyPlan {
            get {
                return new BillingPlan {
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
            }
        }

        public static BillingPlan LargePlan {
            get {
                return new BillingPlan {
                    Id = "EX_LARGE",
                    Name = "Large",
                    Description = "Large ($99/month)",
                    Price = 99,
                    MaxProjects = -1,
                    MaxUsers = -1,
                    RetentionDays = 365,
                    MaxEventsPerMonth = 150000,
                    HasPremiumFeatures = true
                };
            }
        }

        public static BillingPlan LargeYearlyPlan {
            get {
                return new BillingPlan {
                    Id = "EX_LARGE_YEARLY",
                    Name = "Large (Yearly)",
                    Description = "Large Yearly ($1,089/year - Save $99)",
                    Price = 1089,
                    MaxProjects = -1,
                    MaxUsers = -1,
                    RetentionDays = 365,
                    MaxEventsPerMonth = 150000,
                    HasPremiumFeatures = true
                };
            }
        }

        public static BillingPlan UnlimitedPlan {
            get {
                return new BillingPlan {
                    Id = "EX_UNLIMITED",
                    Name = "Unlimited",
                    Description = "Unlimited",
                    IsHidden = true,
                    Price = 0,
                    MaxProjects = -1,
                    MaxUsers = -1,
                    RetentionDays = -1,
                    MaxEventsPerMonth = -1,
                    HasPremiumFeatures = true
                };
            }
        }

        public static readonly BillingPlan[] Plans = { FreePlan, SmallYearlyPlan, MediumYearlyPlan, LargeYearlyPlan, SmallPlan, MediumPlan, LargePlan, UnlimitedPlan };
    }
}