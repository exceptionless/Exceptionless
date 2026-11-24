using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Tests.Extensions;
using Foundatio.Repositories;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class TrialPlanLimitEndpointTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _billingPlans;
    private readonly SampleDataService _sampleData;
    private readonly ProjectData _projectData;
    private readonly TrialExpirationJob _job;

    public TrialPlanLimitEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _tokenRepository = GetService<ITokenRepository>();
        _billingManager = GetService<BillingManager>();
        _billingPlans = GetService<BillingPlans>();
        _sampleData = GetService<SampleDataService>();
        _projectData = GetService<ProjectData>();
        _job = GetService<TrialExpirationJob>();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ExpiredTrial_OverFreeLimits_ResumesOnlyAfterBothLimitsAreMet(bool removeProjectFirst, bool pendingInvitation)
    {
        // Arrange
        await _sampleData.CreateDataAsync();
        await RefreshDataAsync();

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID) ?? throw new InvalidOperationException("Free organization was not created.");
        _billingManager.ApplyTrialPlan(organization);
        organization.TrialEndsUtc = TimeProvider.GetUtcNow().UtcDateTime;
        if (pendingInvitation)
        {
            organization.Invites.Add(new Invite { EmailAddress = SampleDataService.TEST_ORG_USER_EMAIL, Token = StringExtensions.GetNewToken(), DateAdded = TimeProvider.GetUtcNow().UtcDateTime });
        }
        else
        {
            var member = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL) ?? throw new InvalidOperationException("Test user was not created.");
            member.OrganizationIds.Add(organization.Id);
            await _userRepository.SaveAsync(member, command => command.ImmediateConsistency().Cache());
        }
        await _organizationRepository.SaveAsync(organization, command => command.ImmediateConsistency().Cache());
        var secondProject = _projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        await _projectRepository.AddAsync(secondProject, command => command.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);
        var expired = await _organizationRepository.GetByIdAsync(organization.Id);
        var suspendedToken = await _tokenRepository.GetByIdAsync(SampleDataService.FREE_API_KEY);
        bool repeatedCheckChangedSuspension = await _billingManager.UpdatePlanLimitSuspensionAsync(expired!);
        await (removeProjectFirst ? RemoveProjectAsync(secondProject.Id) : RemoveUserOrInviteAsync());
        var partiallyRemediated = await _organizationRepository.GetByIdAsync(organization.Id);
        await (removeProjectFirst ? RemoveUserOrInviteAsync() : RemoveProjectAsync(secondProject.Id));
        var remediated = await _organizationRepository.GetByIdAsync(organization.Id);
        var remainingProject = await _projectRepository.GetByIdAsync(SampleDataService.FREE_PROJECT_ID);
        var resumedToken = await _tokenRepository.GetByIdAsync(SampleDataService.FREE_API_KEY);

        // Assert
        Assert.NotNull(expired);
        Assert.Equal("EX_FREE", expired.PlanId);
        Assert.Equal(1, expired.MaxUsers);
        Assert.Equal(1, expired.MaxProjects);
        Assert.True(expired.IsSuspended);
        Assert.Equal(SuspensionCode.PlanLimit, expired.SuspensionCode);
        Assert.False(repeatedCheckChangedSuspension);
        Assert.True(suspendedToken?.IsSuspended);
        Assert.True(partiallyRemediated?.IsSuspended);
        Assert.Equal(removeProjectFirst ? "Please remove 1 user or pending invitation and try again." : "Please remove 1 project and try again.", partiallyRemediated?.SuspensionNotes);
        Assert.NotNull(remediated);
        Assert.False(remediated.IsSuspended);
        Assert.Null(remediated.SuspensionCode);
        Assert.False(resumedToken?.IsSuspended);
        Assert.NotNull(remainingProject);
    }

    private async Task RemoveUserOrInviteAsync()
    {
        using var response = await SendRequestAsync(request => request.AsFreeOrganizationUser().Delete()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "users", SampleDataService.TEST_ORG_USER_EMAIL).StatusCodeShouldBeOk());
    }

    private async Task RemoveProjectAsync(string projectId)
    {
        using var response = await SendRequestAsync(request => request.AsFreeOrganizationUser().Delete()
            .AppendPaths("projects", projectId).StatusCodeShouldBeAccepted());
    }

    [Theory]
    [InlineData(SuspensionCode.Billing)]
    [InlineData(SuspensionCode.Abuse)]
    [InlineData(SuspensionCode.Overage)]
    [InlineData(SuspensionCode.Other)]
    public async Task RecheckPlanLimitSuspensionAsync_AnotherSuspensionReason_PreservesSuspension(SuspensionCode code)
    {
        // Arrange
        var organization = new Organization { Name = "Manual suspension", IsSuspended = true, SuspensionCode = code,
            SuspendedByUserId = TestConstants.UserId, SuspensionDate = TimeProvider.GetUtcNow().UtcDateTime, SuspensionNotes = "Manual suspension reason" };
        _billingManager.ApplyBillingPlan(organization, _billingPlans.FreePlan);
        await _organizationRepository.AddAsync(organization);

        // Act
        bool changed = await _billingManager.UpdatePlanLimitSuspensionAsync(organization);
        await _billingManager.RecheckPlanLimitSuspensionAsync(organization.Id);
        var saved = await _organizationRepository.GetByIdAsync(organization.Id);

        // Assert
        Assert.False(changed);
        Assert.True(saved?.IsSuspended);
        Assert.Equal(code, saved?.SuspensionCode);
        Assert.Equal("Manual suspension reason", saved?.SuspensionNotes);
    }

    [Fact]
    public async Task UpdatePlanLimitSuspensionAsync_UnsuspendedOrganizationWithinLimits_DoesNotReportAChange()
    {
        // Arrange
        var organization = new Organization { Name = "Within Free limits" };
        _billingManager.ApplyBillingPlan(organization, _billingPlans.FreePlan);
        await _organizationRepository.AddAsync(organization);

        // Act
        bool changed = await _billingManager.UpdatePlanLimitSuspensionAsync(organization);

        // Assert
        Assert.False(changed);
        Assert.False(organization.IsSuspended);
        Assert.Null(organization.SuspensionCode);
    }
}
