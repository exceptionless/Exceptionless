using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
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
        var userData = GetService<UserData>();
        var projectData = GetService<ProjectData>();
        var tokenData = GetService<TokenData>();
        var organization = new Organization { Name = "Over-limit trial" };
        billing.ApplyTrialPlan(organization);
        organization.TrialEndsUtc = TimeProvider.GetUtcNow().UtcDateTime;
        if (pendingInvitation)
            organization.Invites.Add(new Invite { EmailAddress = "member@example.test", Token = StringExtensions.GetNewToken(), DateAdded = TimeProvider.GetUtcNow().UtcDateTime });
        await organizations.AddAsync(organization, command => command.ImmediateConsistency());
        var owner = userData.GenerateUser(generateId: true, organizationId: organization.Id,
            emailAddress: "owner@example.test", roles: [AuthorizationRoles.User, AuthorizationRoles.Client]);
        owner.Salt = StringExtensions.GetNewToken();
        owner.Password = "Trial password".ToSaltedHash(owner.Salt);
        owner.MarkEmailAddressVerified();
        var member = userData.GenerateUser(generateId: true, organizationId: organization.Id, emailAddress: "member@example.test");
        member.MarkEmailAddressVerified();
        await users.AddAsync(pendingInvitation ? [owner] : [owner, member], command => command.ImmediateConsistency());
        var firstProject = projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        var secondProject = projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        await projects.AddAsync([firstProject, secondProject], command => command.ImmediateConsistency());
        var tokens = GetService<ITokenRepository>();
        var token = tokenData.GenerateToken(generateId: true, organizationId: organization.Id, projectId: firstProject.Id);
        await tokens.AddAsync(token, command => command.ImmediateConsistency());
        var job = GetService<TrialExpirationJob>();
        Func<Task> removeMember = async () =>
        {
            using var response = await SendRequestAsync(request => request.BasicAuthorization(owner.EmailAddress, "Trial password")
                .Delete().AppendPaths("organizations", organization.Id, "users", member.EmailAddress).StatusCodeShouldBeOk());
        };
        Func<Task> removeProject = async () =>
        {
            using var response = await SendRequestAsync(request => request.BasicAuthorization(owner.EmailAddress, "Trial password")
                .Delete().AppendPaths("projects", secondProject.Id).StatusCodeShouldBeAccepted());
        };

        // Act
        await job.RunAsync(TestCancellationToken);
        var expired = await organizations.GetByIdAsync(organization.Id);
        var suspendedToken = await tokens.GetByIdAsync(token.Id);
        bool repeatedCheckChangedSuspension = await billing.UpdatePlanLimitSuspensionAsync(expired!);
        await (removeProjectFirst ? removeProject() : removeMember());
        var partiallyRemediated = await organizations.GetByIdAsync(organization.Id);
        await (removeProjectFirst ? removeMember() : removeProject());
        var remediated = await organizations.GetByIdAsync(organization.Id);
        var remainingProject = await projects.GetByIdAsync(firstProject.Id);
        var resumedToken = await tokens.GetByIdAsync(token.Id);

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
