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
        var organization = new Organization { Name = "Over-limit trial" };
        billing.ApplyTrialPlan(organization);
        organization.TrialEndsUtc = TimeProvider.GetUtcNow().UtcDateTime;
        if (pendingInvitation)
            organization.Invites.Add(new Invite { EmailAddress = "member@example.test", Token = StringExtensions.GetNewToken(), DateAdded = TimeProvider.GetUtcNow().UtcDateTime });
        await organizations.AddAsync(organization, command => command.ImmediateConsistency());
        var owner = GetService<UserData>().GenerateUser(generateId: true, organizationId: organization.Id,
            emailAddress: "owner@example.test", roles: [AuthorizationRoles.User, AuthorizationRoles.Client]);
        owner.Salt = StringExtensions.GetNewToken();
        owner.Password = "Trial password".ToSaltedHash(owner.Salt);
        owner.MarkEmailAddressVerified();
        var member = GetService<UserData>().GenerateUser(generateId: true, organizationId: organization.Id, emailAddress: "member@example.test");
        member.MarkEmailAddressVerified();
        await users.AddAsync(pendingInvitation ? [owner] : [owner, member], command => command.ImmediateConsistency());
        var firstProject = GetService<ProjectData>().GenerateProject(generateId: true, organizationId: organization.Id);
        var secondProject = GetService<ProjectData>().GenerateProject(generateId: true, organizationId: organization.Id);
        await projects.AddAsync([firstProject, secondProject], command => command.ImmediateConsistency());
        var tokens = GetService<ITokenRepository>();
        var token = GetService<TokenData>().GenerateToken(generateId: true, organizationId: organization.Id, projectId: firstProject.Id);
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
        Assert.True(suspendedToken?.IsSuspended);
        Assert.True(partiallyRemediated?.IsSuspended);
        Assert.NotNull(remediated);
        Assert.False(remediated.IsSuspended);
        Assert.Null(remediated.SuspensionCode);
        Assert.False(resumedToken?.IsSuspended);
        Assert.NotNull(remainingProject);
    }

    [Theory]
    [InlineData(SuspensionCode.Billing)]
    [InlineData(SuspensionCode.Abuse)]
    public async Task RecheckPlanLimitSuspensionAsync_AnotherSuspensionReason_PreservesSuspension(SuspensionCode code)
    {
        // Arrange
        var organization = new Organization { Name = "Manual suspension", IsSuspended = true, SuspensionCode = code,
            SuspendedByUserId = TestConstants.UserId, SuspensionDate = TimeProvider.GetUtcNow().UtcDateTime };
        var billing = GetService<BillingManager>();
        billing.ApplyBillingPlan(organization, GetService<BillingPlans>().FreePlan);
        var repository = GetService<IOrganizationRepository>();
        await repository.AddAsync(organization);

        // Act
        await billing.RecheckPlanLimitSuspensionAsync(organization.Id);
        var saved = await repository.GetByIdAsync(organization.Id);

        // Assert
        Assert.True(saved?.IsSuspended);
        Assert.Equal(code, saved?.SuspensionCode);
    }
}
