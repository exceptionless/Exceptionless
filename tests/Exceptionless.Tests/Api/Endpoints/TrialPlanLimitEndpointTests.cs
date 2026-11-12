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

public sealed class TrialPlanLimitEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ExpiredTrial_OverFreeLimits_ResumesOnlyAfterBothLimitsAreMet(bool removeProjectFirst, bool pendingInvitation)
    {
        // Arrange
        var organizations = GetService<IOrganizationRepository>();
        var users = GetService<IUserRepository>();
        var projects = GetService<IProjectRepository>();
        var billing = GetService<BillingManager>();
        var sampleData = GetService<SampleDataService>();
        var projectData = GetService<ProjectData>();
        var tokens = GetService<ITokenRepository>();
        var job = GetService<TrialExpirationJob>();
        await sampleData.CreateDataAsync();
        await RefreshDataAsync();

        var organization = await organizations.GetByIdAsync(SampleDataService.FREE_ORG_ID) ?? throw new InvalidOperationException("Free organization was not created.");
        billing.ApplyTrialPlan(organization);
        organization.TrialEndsUtc = TimeProvider.GetUtcNow().UtcDateTime;
        if (pendingInvitation)
        {
            organization.Invites.Add(new Invite { EmailAddress = SampleDataService.TEST_ORG_USER_EMAIL, Token = StringExtensions.GetNewToken(), DateAdded = TimeProvider.GetUtcNow().UtcDateTime });
        }
        else
        {
            var member = await users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL) ?? throw new InvalidOperationException("Test user was not created.");
            member.OrganizationIds.Add(organization.Id);
            await users.SaveAsync(member, command => command.ImmediateConsistency().Cache());
        }
        await organizations.SaveAsync(organization, command => command.ImmediateConsistency().Cache());
        var secondProject = projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        await projects.AddAsync(secondProject, command => command.ImmediateConsistency());

        // Act
        await job.RunAsync(TestCancellationToken);
        var expired = await organizations.GetByIdAsync(organization.Id);
        var suspendedToken = await tokens.GetByIdAsync(SampleDataService.FREE_API_KEY);
        bool repeatedCheckChangedSuspension = await billing.UpdatePlanLimitSuspensionAsync(expired!);
        await (removeProjectFirst ? RemoveProjectAsync(secondProject.Id) : RemoveUserOrInviteAsync());
        var partiallyRemediated = await organizations.GetByIdAsync(organization.Id);
        await (removeProjectFirst ? RemoveUserOrInviteAsync() : RemoveProjectAsync(secondProject.Id));
        var remediated = await organizations.GetByIdAsync(organization.Id);
        var remainingProject = await projects.GetByIdAsync(SampleDataService.FREE_PROJECT_ID);
        var resumedToken = await tokens.GetByIdAsync(SampleDataService.FREE_API_KEY);

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
        var billing = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        billing.ApplyBillingPlan(organization, plans.FreePlan);
        var repository = GetService<IOrganizationRepository>();
        await repository.AddAsync(organization);

        // Act
        bool changed = await billing.UpdatePlanLimitSuspensionAsync(organization);
        await billing.RecheckPlanLimitSuspensionAsync(organization.Id);
        var saved = await repository.GetByIdAsync(organization.Id);

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
        var billing = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        var repository = GetService<IOrganizationRepository>();
        billing.ApplyBillingPlan(organization, plans.FreePlan);
        await repository.AddAsync(organization);

        // Act
        bool changed = await billing.UpdatePlanLimitSuspensionAsync(organization);

        // Assert
        Assert.False(changed);
        Assert.False(organization.IsSuspended);
        Assert.Null(organization.SuspensionCode);
    }
}
