using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Mediator;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class OrganizationHandlerTests : TestWithServices
{
    public OrganizationHandlerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Handle_CreateOrganizationTrialClaimFails_DoesNotPersistTrialAndAllowsRetry()
    {
        // Arrange
        var persistedUser = new User
        {
            Id = TestConstants.UserId,
            FullName = "Trial Owner",
            EmailAddress = "trial-owner@example.test",
            IsEmailAddressVerified = true
        };
        var context = new DefaultHttpContext { RequestAborted = TestCancellationToken };
        context.Request.SetUser(persistedUser);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByIdAsync(persistedUser.Id, It.IsAny<CommandOptionsDescriptor<User>>()))
            .ReturnsAsync(() => persistedUser with { OrganizationIds = new HashSet<string>(persistedUser.OrganizationIds) });
        users.Setup(repository => repository.SaveAsync(It.IsAny<User>(), It.IsAny<CommandOptionsDescriptor<User>>()))
            .ReturnsAsync((User user, CommandOptionsDescriptor<User> _) => persistedUser = user);

        var persistedOrganizations = new List<Organization>();
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        organizations.Setup(repository => repository.FindAsync(It.IsAny<RepositoryQueryDescriptor<Organization>>(), It.IsAny<CommandOptionsDescriptor<Organization>>()))
            .ReturnsAsync(FindResults<Organization>.Empty);
        organizations.Setup(repository => repository.CountAsync(It.IsAny<RepositoryQueryDescriptor<Organization>>(), It.IsAny<CommandOptionsDescriptor<Organization>>()))
            .ReturnsAsync(CountResult.Empty);
        organizations.Setup(repository => repository.AddAsync(It.IsAny<Organization>(), It.IsAny<CommandOptionsDescriptor<Organization>>()))
            .ReturnsAsync((Organization organization, CommandOptionsDescriptor<Organization> _) =>
            {
                organization.Id = TestConstants.OrganizationId;
                persistedOrganizations.Add(organization);
                return organization;
            });
        organizations.Setup(repository => repository.GetByIdAsync(TestConstants.OrganizationId, It.IsAny<CommandOptionsDescriptor<Organization>>()))
            .ReturnsAsync(() => persistedOrganizations.Single());

        bool failClaim = true;
        TrialClaim? persistedClaim = null;
        var claimFailure = new IOException("Trial claim store is unavailable.");
        var claims = new Mock<ITrialClaimRepository>(MockBehavior.Strict);
        string claimId = TrialEmailAddress.GetClaimId(persistedUser.EmailAddress, GetService<AppOptions>().BillingOptions.TrialClaimSecret!);
        claims.Setup(repository => repository.ExistsAsync(claimId, It.IsAny<ICommandOptions>()))
            .ReturnsAsync(() => persistedClaim is not null);
        claims.Setup(repository => repository.AddAsync(It.IsAny<TrialClaim>(), It.IsAny<ICommandOptions>()))
            .Returns((TrialClaim claim, ICommandOptions _) =>
            {
                if (failClaim)
                    return Task.FromException<TrialClaim>(claimFailure);

                persistedClaim = claim;
                return Task.FromResult(claim);
            });
        var handler = CreateHandler(organizations.Object, users.Object, claims.Object, context);
        var message = new CreateOrganization(new NewOrganization { Name = "Trial Organization" }, context);

        // Act
        var exception = await Record.ExceptionAsync(async () => _ = await handler.Handle(message));

        // Assert
        Assert.Empty(persistedOrganizations);
        Assert.Empty(persistedUser.OrganizationIds);
        Assert.Null(persistedUser.TrialStartedUtc);
        Assert.Null(persistedClaim);
        Assert.Same(claimFailure, exception);

        // Arrange: recover the claim store and retry as the same user.
        failClaim = false;

        // Act
        var result = await handler.Handle(message);

        // Assert
        Assert.Equal(ResultStatus.Created, result.Status);
        var organization = Assert.Single(persistedOrganizations);
        Assert.Equal(GetService<BillingPlans>().TrialPlan.Id, organization.PlanId);
        Assert.Equal(organization.Id, Assert.Single(persistedUser.OrganizationIds));
        Assert.NotNull(persistedUser.TrialStartedUtc);
        Assert.Equal(claimId, Assert.IsType<TrialClaim>(persistedClaim).Id);
    }

    private OrganizationHandler CreateHandler(IOrganizationRepository organizations, IUserRepository users, ITrialClaimRepository claims, HttpContext context)
    {
        var options = GetService<AppOptions>();
        options.StripeOptions.StripeApiKey = "sk_test_local";
        var cache = GetService<ICacheClient>();
        var locks = GetService<ILockProvider>();
        var plans = GetService<BillingPlans>();
        var settings = new SystemSettingsService(() => Task.FromResult<SystemSettings?>(null), _ => Task.CompletedTask, options, TimeProvider);
        var billingManager = new BillingManager(cache, locks, organizations, claims, users, settings, plans, options, TimeProvider);
        var projects = Mock.Of<IProjectRepository>();
        var publisher = GetService<IMessagePublisher>();
        var usageService = new UsageService(organizations, projects, cache, publisher, GetService<NotificationService>(), TimeProvider, Log);
        var stripeBillingClient = Mock.Of<IStripeBillingClient>();
        var organizationService = new OrganizationService(organizations, projects, Mock.Of<ISavedViewRepository>(),
            Mock.Of<ITokenRepository>(), users, Mock.Of<IWebHookRepository>(), stripeBillingClient, usageService, Log);
        var links = new Mock<LinkGenerator>(MockBehavior.Strict);
        links.Setup(generator => generator.GetUriByAddress(context, "GetOrganizationById", It.IsAny<RouteValueDictionary>(),
                It.IsAny<RouteValueDictionary>(), It.IsAny<string>(), It.IsAny<HostString?>(), It.IsAny<PathString?>(),
                It.IsAny<FragmentString>(), It.IsAny<LinkOptions>()))
            .Returns("http://localhost/api/v2/organizations/" + TestConstants.OrganizationId);

        return new OrganizationHandler(
            organizationService,
            organizations,
            cache,
            Mock.Of<IEventRepository>(),
            users,
            projects,
            billingManager,
            plans,
            usageService,
            stripeBillingClient,
            GetService<IMailer>(),
            publisher,
            new ApiMapper(TimeProvider),
            options,
            TimeProvider,
            new HttpContextAccessor { HttpContext = context },
            links.Object,
            Log);
    }
}
