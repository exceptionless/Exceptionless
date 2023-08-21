﻿using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Utility;

public class SampleDataService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _billingPlans;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SampleDataService> _logger;

    public const string TEST_USER_EMAIL = "test@localhost";
    public const string TEST_USER_PASSWORD = "tester";
    public const string TEST_ORG_ID = "537650f3b77efe23a47914f3";
    public const string TEST_PROJECT_ID = "537650f3b77efe23a47914f4";
    public const string TEST_ROCKET_SHIP_PROJECT_ID = "537650f3b77efe23a47914f7";
    public const string TEST_API_KEY = "LhhP1C9gijpSKCslHHCvwdSIz298twx271nTest";
    public const string TEST_ROCKET_SHIP_API_KEY = "LhhP1C9gijpSKCslHHCvwdSIz298twx271oTest";
    public const string TEST_USER_API_KEY = "5f8aT5j0M1SdWCMOiJKCrlDNHMI38LjCH4LTTest";
    public const string TEST_ORG_USER_EMAIL = "org@localhost";
    public const string TEST_ORG_USER_PASSWORD = "tester";
    public const string FREE_USER_EMAIL = "free@localhost";
    public const string FREE_USER_PASSWORD = "tester";
    public const string FREE_ORG_ID = "537650f3b77efe23a47914f5";
    public const string FREE_PROJECT_ID = "537650f3b77efe23a47914f6";
    public const string FREE_API_KEY = "LhhP1C9gijpSKCslHHCvwdSIz298twx271n1Free";
    public const string FREE_USER_API_KEY = "5f8aT5j0M1SdWCMOiJKCrlDNHMI37LjCH4LTFree";
    public const string INTERNAL_API_KEY = "Bx7JgglstPG544R34Tw9T7RlCed3OIwtYXVeyhT2";
    public const string INTERNAL_PROJECT_ID = "54b56e480ef9605a88a13153";

    public SampleDataService(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        ITokenRepository tokenRepository,
        BillingManager billingManager,
        BillingPlans billingPlans,
        ILoggerFactory loggerFactory
    )
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _billingManager = billingManager;
        _billingPlans = billingPlans;
        _logger = loggerFactory.CreateLogger<SampleDataService>();
    }

    public async Task CreateDataAsync()
    {
        if (await _userRepository.GetByEmailAddressAsync(TEST_USER_EMAIL).AnyContext() is not null)
            return;

        var user = new User
        {
            FullName = "Test User",
            EmailAddress = TEST_USER_EMAIL
        };

        user.CreateVerifyEmailAddressToken();
        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);
        user.Roles.Add(AuthorizationRoles.GlobalAdmin);

        user.Salt = StringExtensions.GetRandomString(16);
        user.Password = TEST_USER_PASSWORD.ToSaltedHash(user.Salt);

        user = await _userRepository.AddAsync(user, o => o.ImmediateConsistency().Cache()).AnyContext();
        _logger.LogDebug("Created Global Admin {FullName} - {EmailAddress}", user.FullName, user.EmailAddress);
        await CreateOrganizationAndProjectAsync(user).AnyContext();
        await CreateInternalOrganizationAndProjectAsync(user.Id).AnyContext();
        await CreateOrganizationAdminUserAsync().AnyContext();
        await CreateFreeOrganizationAndProjectAsync().AnyContext();
    }

    public async Task CreateOrganizationAdminUserAsync()
    {
        if (await _userRepository.GetByEmailAddressAsync(TEST_ORG_USER_EMAIL).AnyContext() is not null)
            return;

        var user = new User
        {
            FullName = "Test Org User",
            EmailAddress = TEST_ORG_USER_EMAIL
        };

        user.CreateVerifyEmailAddressToken();
        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);

        user.Salt = StringExtensions.GetRandomString(16);
        user.Password = TEST_ORG_USER_PASSWORD.ToSaltedHash(user.Salt);

        user.OrganizationIds.Add(TEST_ORG_ID);

        user = await _userRepository.AddAsync(user, o => o.ImmediateConsistency().Cache()).AnyContext();
        _logger.LogDebug("Created Org Admin {FullName} - {EmailAddress}", user.FullName, user.EmailAddress);
    }

    public async Task CreateOrganizationAndProjectAsync(User user)
    {
        if (await _tokenRepository.ExistsAsync(TEST_API_KEY).AnyContext())
            return;

        var organization = new Organization { Id = TEST_ORG_ID, Name = "Acme" };
        _billingManager.ApplyBillingPlan(organization, _billingPlans.UnlimitedPlan, user);
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache()).AnyContext();

        var disintegratingPistolProject = new Project
        {
            Id = TEST_PROJECT_ID,
            Name = "Disintegrating Pistol",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks
        };
        disintegratingPistolProject.Configuration.Settings.Add("IncludeConditionalData", "true");
        disintegratingPistolProject.AddDefaultNotificationSettings(user.Id);

        var rocketShipProject = new Project
        {
            Id = TEST_ROCKET_SHIP_PROJECT_ID,
            Name = "Rocket Ship",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks
        };

        await _projectRepository.AddAsync(new List<Project> {
            disintegratingPistolProject,
            rocketShipProject
        }, o => o.ImmediateConsistency().Cache()).AnyContext();

        await _tokenRepository.AddAsync(new List<Token>()
        {
                new()
                {
                    Id = TEST_API_KEY,
                    OrganizationId = organization.Id,
                    ProjectId = disintegratingPistolProject.Id,
                    CreatedUtc = SystemClock.UtcNow,
                    UpdatedUtc = SystemClock.UtcNow,
                    Type = TokenType.Access
                },
                new()
                {
                    Id = TEST_ROCKET_SHIP_API_KEY,
                    OrganizationId = organization.Id,
                    ProjectId = rocketShipProject.Id,
                    CreatedUtc = SystemClock.UtcNow,
                    UpdatedUtc = SystemClock.UtcNow,
                    Type = TokenType.Access
                },
                new()
                {
                    Id = TEST_USER_API_KEY,
                    UserId = user.Id,
                    CreatedUtc = SystemClock.UtcNow,
                    UpdatedUtc = SystemClock.UtcNow,
                    Type = TokenType.Access
                }
            }, o => o.ImmediateConsistency().Cache()).AnyContext();

        user.OrganizationIds.Add(organization.Id);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache()).AnyContext();
        _logger.LogDebug("Created Organization {OrganizationName} and Projects {DisintegratingPistolProjectName}, {RocketShipProjectName}", organization.Name, disintegratingPistolProject.Name, rocketShipProject.Name);
    }

    public async Task CreateFreeOrganizationAndProjectAsync()
    {
        if (await _userRepository.GetByEmailAddressAsync(FREE_USER_EMAIL).AnyContext() is not null)
            return;

        var user = new User
        {
            FullName = "Free User",
            EmailAddress = FREE_USER_EMAIL
        };

        user.CreateVerifyEmailAddressToken();
        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);

        user.Salt = StringExtensions.GetRandomString(16);
        user.Password = FREE_USER_PASSWORD.ToSaltedHash(user.Salt);

        user = await _userRepository.AddAsync(user, o => o.ImmediateConsistency().Cache()).AnyContext();

        if (await _tokenRepository.ExistsAsync(FREE_API_KEY).AnyContext())
            return;

        var organization = new Organization { Id = FREE_ORG_ID, Name = "Free Plan Organization" };
        _billingManager.ApplyBillingPlan(organization, _billingPlans.FreePlan, user);
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache()).AnyContext();

        var project = new Project
        {
            Id = FREE_PROJECT_ID,
            Name = "Free Plan Project",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks
        };
        project.Configuration.Settings.Add("IncludeConditionalData", "true");
        project.AddDefaultNotificationSettings(user.Id);
        project = await _projectRepository.AddAsync(project, o => o.ImmediateConsistency().Cache()).AnyContext();

        await _tokenRepository.AddAsync(new List<Token>()
        {
                new()
                {
                    Id = FREE_API_KEY,
                    OrganizationId = organization.Id,
                    ProjectId = project.Id,
                    CreatedUtc = SystemClock.UtcNow,
                    UpdatedUtc = SystemClock.UtcNow,
                    Type = TokenType.Access
                },
                    new()
                    {
                    Id = FREE_USER_API_KEY,
                    UserId = user.Id,
                    CreatedUtc = SystemClock.UtcNow,
                    UpdatedUtc = SystemClock.UtcNow,
                    Type = TokenType.Access
                }
            }, o => o.ImmediateConsistency().Cache()).AnyContext();

        user.OrganizationIds.Add(organization.Id);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache()).AnyContext();
        _logger.LogDebug("Created Free Organization {OrganizationName} and Project {ProjectName}", organization.Name, project.Name);
    }

    public async Task CreateInternalOrganizationAndProjectAsync(string userId)
    {
        if (await _tokenRepository.GetByIdAsync(INTERNAL_API_KEY).AnyContext() is not null)
            return;

        var user = await _userRepository.GetByIdAsync(userId, o => o.Cache()).AnyContext();
        var organization = new Organization { Name = "Exceptionless" };
        _billingManager.ApplyBillingPlan(organization, _billingPlans.UnlimitedPlan, user);
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache()).AnyContext();

        var project = new Project
        {
            Id = INTERNAL_PROJECT_ID,
            Name = "API",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks
        };
        project.AddDefaultNotificationSettings(userId);
        project = await _projectRepository.AddAsync(project, o => o.ImmediateConsistency().Cache()).AnyContext();

        await _tokenRepository.AddAsync(new Token
        {
            Id = INTERNAL_API_KEY,
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            CreatedBy = userId,
            CreatedUtc = SystemClock.UtcNow,
            UpdatedUtc = SystemClock.UtcNow,
            Type = TokenType.Access
        }, o => o.ImmediateConsistency()).AnyContext();

        user.OrganizationIds.Add(organization.Id);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache()).AnyContext();
        _logger.LogDebug("Created Internal Organization {OrganizationName} and Project {ProjectName}", organization.Name, project.Name);
    }
}
