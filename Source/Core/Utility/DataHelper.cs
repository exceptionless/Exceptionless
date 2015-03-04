using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Admin;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class DataHelper {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;

        public const string TEST_USER_EMAIL = "test@exceptionless.io";
        public const string TEST_USER_PASSWORD = "tester";
        public const string TEST_ORG_ID = "537650f3b77efe23a47914f3";
        public const string TEST_PROJECT_ID = "537650f3b77efe23a47914f4";
        public const string TEST_API_KEY = "LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw";
        public const string TEST_USER_API_KEY = "5f8aT5j0M1SdWCMOiJKCrlDNHMI38LjCH4LTWqGp";
        public const string INTERNAL_API_KEY = "Bx7JgglstPG544R34Tw9T7RlCed3OIwtYXVeyhT2";
        public const string INTERNAL_PROJECT_ID = "54b56e480ef9605a88a13153";

        public DataHelper(IOrganizationRepository organizationRepository,
            IProjectRepository projectRepository,
            IUserRepository userRepository,
            IEventRepository eventRepository,
            IStackRepository stackRepository,
            ITokenRepository tokenRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _tokenRepository = tokenRepository;
        }

        public async Task ResetProjectDataAsync(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return;

            Project project = _projectRepository.GetById(projectId);
            if (project == null)
                return;

            try {
                await _eventRepository.RemoveAllByProjectIdsAsync(new [] { projectId });
                await _stackRepository.RemoveAllByProjectIdsAsync(new [] { projectId });

                _projectRepository.Save(project);
            } catch (Exception e) {
                Log.Error().Project(projectId).Exception(e).Message("Error resetting project data.").Write();
                throw;
            }
        }

        public async Task ResetStackDataAsync(string stackId) {
            if (String.IsNullOrEmpty(stackId))
                return;

            Stack stack = _stackRepository.GetById(stackId);
            if (stack == null)
                return;

            try {
                stack.TotalOccurrences = 0;
                stack.LastOccurrence = DateTime.MinValue.ToUniversalTime();
                stack.FirstOccurrence = DateTime.MinValue.ToUniversalTime();
                _stackRepository.Save(stack);

                await _eventRepository.RemoveAllByStackIdsAsync(new[] { stackId });
            } catch (Exception e) {
                Log.Error().Project(stack.ProjectId).Exception(e).Message("Error resetting stack data.").Write();
                throw;
            }
        }

        public string CreateDefaultOrganizationAndProject(User user) {
            string organizationId = user.OrganizationIds.FirstOrDefault();
            if (!String.IsNullOrEmpty(organizationId)) {
                var defaultProject = _projectRepository.GetByOrganizationId(user.OrganizationIds.First(), useCache: true).FirstOrDefault();
                if (defaultProject != null)
                    return defaultProject.Id;
            } else {
                var organization = new Organization {
                    Name = "Default Organization"
                };
                BillingManager.ApplyBillingPlan(organization, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, user);
                _organizationRepository.Add(organization);
                organizationId = organization.Id;
            }

            var project = new Project { Name = "Default Project", OrganizationId = organizationId };
            project.NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            project.AddDefaultOwnerNotificationSettings(user.Id);
            project = _projectRepository.Add(project);
            
            _tokenRepository.Add(new Token {
                Id = StringExtensions.GetNewToken(),
                OrganizationId = organizationId,
                ProjectId = project.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                Type = TokenType.Access
            });

            if (!user.OrganizationIds.Contains(organizationId)) {
                user.OrganizationIds.Add(organizationId);
                _userRepository.Save(user, true);
            }

            return project.Id;
        }

        public void CreateTestData() {
            if (_userRepository.GetByEmailAddress(TEST_USER_EMAIL) != null)
                return;

            var user = new User {
                FullName = "Test User", 
                EmailAddress = TEST_USER_EMAIL,
                IsEmailAddressVerified = true
            };
            user.Roles.Add(AuthorizationRoles.Client);
            user.Roles.Add(AuthorizationRoles.User);
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);

            user.Salt = StringExtensions.GetRandomString(16);
            user.Password = TEST_USER_PASSWORD.ToSaltedHash(user.Salt);

            user = _userRepository.Add(user);
            CreateTestOrganizationAndProject(user.Id);
            CreateTestInternalOrganizationAndProject(user.Id);
        }

        public void CreateTestOrganizationAndProject(string userId) {
            if (_tokenRepository.GetById(TEST_API_KEY) != null)
                return;

            User user = _userRepository.GetById(userId, true);
            var organization = new Organization { Id = TEST_ORG_ID, Name = "Acme" };
            BillingManager.ApplyBillingPlan(organization, BillingManager.UnlimitedPlan, user);
            organization = _organizationRepository.Add(organization);

            var project = new Project { Id = TEST_PROJECT_ID, Name = "Disintegrating Pistol", OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            project.Configuration.Settings.Add("IncludeConditionalData", "true");
            project.AddDefaultOwnerNotificationSettings(userId);
            project = _projectRepository.Add(project, true);

            _tokenRepository.Add(new Token {
                Id = TEST_API_KEY,
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                Type = TokenType.Access
            });

            _tokenRepository.Add(new Token {
                Id = TEST_USER_API_KEY,
                UserId = user.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                Type = TokenType.Access
            });

            user.OrganizationIds.Add(organization.Id);
            _userRepository.Save(user, true);
        }

        public void CreateTestInternalOrganizationAndProject(string userId) {
            if (_tokenRepository.GetById(INTERNAL_API_KEY) != null)
                return;

            User user = _userRepository.GetById(userId, true);
            var organization = new Organization { Name = "Exceptionless" };
            BillingManager.ApplyBillingPlan(organization, BillingManager.UnlimitedPlan, user);
            organization = _organizationRepository.Add(organization);

            var project = new Project { Id = INTERNAL_PROJECT_ID, Name = "API", OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            project.AddDefaultOwnerNotificationSettings(userId);
            project = _projectRepository.Add(project, true);

            _tokenRepository.Add(new Token {
                Id = INTERNAL_API_KEY,
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                Type = TokenType.Access
            });

            user.OrganizationIds.Add(organization.Id);
            _userRepository.Save(user, true);
        }
    }
}