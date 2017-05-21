using System;
using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Utility;

namespace Exceptionless.Core.Utility {
    public class SampleDataService {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;

        public const string TEST_USER_EMAIL = "test@exceptionless.io";
        public const string TEST_USER_PASSWORD = "tester";
        public const string TEST_ORG_ID = "537650f3b77efe23a47914f3";
        public const string TEST_PROJECT_ID = "537650f3b77efe23a47914f4";
        public const string TEST_API_KEY = "LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw";
        public const string TEST_USER_API_KEY = "5f8aT5j0M1SdWCMOiJKCrlDNHMI38LjCH4LTWqGp";
        public const string INTERNAL_API_KEY = "Bx7JgglstPG544R34Tw9T7RlCed3OIwtYXVeyhT2";
        public const string INTERNAL_PROJECT_ID = "54b56e480ef9605a88a13153";

        public SampleDataService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, ITokenRepository tokenRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
        }

        public async Task CreateDataAsync() {
            if (await _userRepository.GetByEmailAddressAsync(TEST_USER_EMAIL).AnyContext() != null)
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

            user = await _userRepository.AddAsync(user, o => o.Cache()).AnyContext();
            await CreateOrganizationAndProjectAsync(user.Id).AnyContext();
            await CreateInternalOrganizationAndProjectAsync(user.Id).AnyContext();
        }

        public async Task CreateOrganizationAndProjectAsync(string userId) {
            if (await _tokenRepository.GetByIdAsync(TEST_API_KEY).AnyContext() != null)
                return;

            var user = await _userRepository.GetByIdAsync(userId, o => o.Cache()).AnyContext();
            var organization = new Organization { Id = TEST_ORG_ID, Name = "Acme" };
            BillingManager.ApplyBillingPlan(organization, BillingManager.UnlimitedPlan, user);
            organization = await _organizationRepository.AddAsync(organization, o => o.Cache()).AnyContext();

            var project = new Project { Id = TEST_PROJECT_ID, Name = "Disintegrating Pistol", OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            project.Configuration.Settings.Add("IncludeConditionalData", "true");
            project.AddDefaultNotificationSettings(userId);
            project = await _projectRepository.AddAsync(project, o => o.Cache()).AnyContext();

            await _tokenRepository.AddAsync(new Token {
                Id = TEST_API_KEY,
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                CreatedUtc = SystemClock.UtcNow,
                UpdatedUtc = SystemClock.UtcNow,
                Type = TokenType.Access
            }).AnyContext();

            await _tokenRepository.AddAsync(new Token {
                Id = TEST_USER_API_KEY,
                UserId = user.Id,
                CreatedUtc = SystemClock.UtcNow,
                UpdatedUtc = SystemClock.UtcNow,
                Type = TokenType.Access
            }).AnyContext();

            user.OrganizationIds.Add(organization.Id);
            await _userRepository.SaveAsync(user, o => o.Cache()).AnyContext();
        }

        public async Task CreateInternalOrganizationAndProjectAsync(string userId) {
            if (await _tokenRepository.GetByIdAsync(INTERNAL_API_KEY).AnyContext() != null)
                return;

            var user = await _userRepository.GetByIdAsync(userId, o => o.Cache()).AnyContext();
            var organization = new Organization { Name = "Exceptionless" };
            BillingManager.ApplyBillingPlan(organization, BillingManager.UnlimitedPlan, user);
            organization = await _organizationRepository.AddAsync(organization, o => o.Cache()).AnyContext();

            var project = new Project { Id = INTERNAL_PROJECT_ID, Name = "API", OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            project.AddDefaultNotificationSettings(userId);
            project = await _projectRepository.AddAsync(project, o => o.Cache()).AnyContext();

            await _tokenRepository.AddAsync(new Token {
                Id = INTERNAL_API_KEY,
                OrganizationId = organization.Id,
                ProjectId = project.Id,
                CreatedUtc = SystemClock.UtcNow,
                UpdatedUtc = SystemClock.UtcNow,
                Type = TokenType.Access
            }).AnyContext();

            user.OrganizationIds.Add(organization.Id);
            await _userRepository.SaveAsync(user, o => o.Cache()).AnyContext();
        }
    }
}
