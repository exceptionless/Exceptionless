using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
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
using Foundatio.Messaging;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class OrganizationHandlerTests : TestWithServices
{
    public OrganizationHandlerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Handle_AddOrganizationUser_UsesPersistedUserWhenMembershipWasConcurrentlyGranted(bool membershipAlreadyGranted, bool userDeleted)
    {
        // Arrange
        var organization = new Organization { Id = "000000000000000000000001", Name = "Test", MaxUsers = -1 };
        var user = new User { Id = "000000000000000000000002", EmailAddress = "added@example.test", Version = "1:1" };
        var persisted = user with { Version = "1:2", OrganizationIds = new HashSet<string>() };
        if (membershipAlreadyGranted)
            persisted.OrganizationIds.Add(organization.Id);
        var context = CreateContext();
        context.Request.SetUser(user);
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        organizations.Setup(repository => repository.GetByIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<Organization>>())).ReturnsAsync(organization);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByOrganizationIdAsync(organization.Id, null)).ReturnsAsync(new FindResults<User>([], 1));
        users.Setup(repository => repository.GetByEmailAddressAsync(user.EmailAddress)).ReturnsAsync(user);
        users.Setup(repository => repository.PatchAsync(user.Id, It.IsAny<IPatchOperation>(), It.IsAny<CommandOptionsDescriptor<User>>()))
            .Returns((Id _, IPatchOperation operation, CommandOptionsDescriptor<User> _) => userDeleted
                ? Task.FromException<bool>(new DocumentNotFoundException("User deleted"))
                : Task.FromResult(((ActionPatch<User>)operation).Actions.Single()(persisted)));
        users.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>())).ReturnsAsync(persisted);
        var publisher = new Mock<IMessagePublisher>();
        var mailer = new Mock<IMailer>();
        var handler = CreateHandler(organizations.Object, users.Object, new Mock<IProjectRepository>().Object,
            new Mock<ISavedViewRepository>().Object, publisher.Object, context, mailer.Object);

        // Act
        var result = await handler.Handle(new AddOrganizationUser(organization.Id, user.EmailAddress, context));

        // Assert
        Assert.Empty(user.OrganizationIds);
        Assert.Equal(userDeleted ? ResultStatus.NotFound : ResultStatus.Success, result.Status);
        if (!userDeleted)
            Assert.Contains(organization.Id, persisted.OrganizationIds);
        mailer.Verify(client => client.SendOrganizationAddedAsync(It.IsAny<User>(), organization, persisted), userDeleted ? Times.Never() : Times.Once());
        publisher.Verify(client => client.PublishAsync(typeof(UserMembershipChanged), It.IsAny<object>(), null, default),
            userDeleted || membershipAlreadyGranted ? Times.Never() : Times.Once());
    }

    [Theory]
    [InlineData("transport", false)]
    [InlineData("conflict", false)]
    [InlineData("conflict", true)]
    [InlineData("missing", false)]
    [InlineData("not-patched", false)]
    public async Task Handle_CreateOrganizationMembershipFails_CleansUpOnlyDefiniteRejections(string outcome, bool cleanupFails)
    {
        // Arrange
        var user = new User { Id = "000000000000000000000002", EmailAddress = "owner@example.test" };
        var context = CreateContext();
        context.Request.SetUser(user);
        var organization = new Organization { Id = "000000000000000000000001", Name = "Test" };
        bool definiteRejection = outcome != "transport";
        Exception failure = outcome switch
        {
            "conflict" => new VersionConflictDocumentException("Membership conflict"),
            "missing" => new DocumentNotFoundException("User deleted"),
            _ => new IOException("Membership transport failure")
        };
        var cleanupFailure = new IOException("Synthetic cleanup failure");
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        organizations.Setup(repository => repository.GetByIdsAsync(It.IsAny<Ids>(), (ICommandOptions?)null)).ReturnsAsync(Array.Empty<Organization>());
        organizations.Setup(repository => repository.AddAsync(It.IsAny<Organization>(), It.IsAny<CommandOptionsDescriptor<Organization>>())).ReturnsAsync(organization);
        if (definiteRejection)
            organizations.Setup(repository => repository.RemoveAsync(organization, (ICommandOptions?)null))
                .Returns(() => cleanupFails ? Task.FromException(cleanupFailure) : Task.CompletedTask);

        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.PatchAsync(user.Id, It.IsAny<IPatchOperation>(), It.IsAny<CommandOptionsDescriptor<User>>()))
            .Returns(() => outcome == "not-patched" ? Task.FromResult(false) : Task.FromException<bool>(failure));
        var handler = CreateHandler(organizations.Object, users.Object,
            new Mock<IProjectRepository>(MockBehavior.Strict).Object,
            new Mock<ISavedViewRepository>(MockBehavior.Strict).Object,
            new Mock<IMessagePublisher>(MockBehavior.Strict).Object, context);

        // Act
        var actual = await Record.ExceptionAsync(async () =>
        {
            _ = await handler.Handle(new CreateOrganization(new NewOrganization { Name = "Test" }, context));
        });

        // Assert
        if (outcome == "not-patched")
            Assert.IsType<DocumentNotFoundException>(actual);
        else
            Assert.Same(failure, actual);
        if (cleanupFails)
        {
            var entry = Assert.Single(Log.LogEntries, entry => entry.LogLevel == LogLevel.Critical);
            Assert.Same(cleanupFailure, entry.Exception);
            Assert.Equal($"Unable to clean up organization {organization.Id} after adding user {user.EmailAddress} failed: {cleanupFailure.Message}", entry.Message);
        }

        organizations.VerifyAll();
        organizations.Verify(repository => repository.RemoveAsync(It.IsAny<Organization>(), It.IsAny<ICommandOptions>()),
            definiteRejection ? Times.Once() : Times.Never());
        users.VerifyAll();
        users.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Handle_RemoveOrganizationUserFailure_PublishesRemovalOnlyAfterPersistence(bool alreadyRemoved, bool persistenceFails)
    {
        // Arrange
        var organization = new Organization { Id = "000000000000000000000001", Name = "Test" };
        var user = new User { Id = "000000000000000000000002", EmailAddress = "removed@example.test" };
        if (!alreadyRemoved)
            user.OrganizationIds.Add(organization.Id);

        var failure = new IOException("Synthetic removal failure");
        var calls = new List<string>();
        var published = new List<UserMembershipChanged>();
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        organizations.Setup(repository => repository.GetByIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<Organization>>())).ReturnsAsync(organization);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByEmailAddressAsync(user.EmailAddress)).ReturnsAsync(user);
        if (!alreadyRemoved)
        {
            users.Setup(repository => repository.GetByOrganizationIdAsync(organization.Id, null)).ReturnsAsync(new FindResults<User>([], 2));
            users.Setup(repository => repository.PatchAsync(user.Id, It.IsAny<IPatchOperation>(), It.IsAny<CommandOptionsDescriptor<User>>()))
                .Callback(() => calls.Add("persist"))
                .Returns(() => persistenceFails ? Task.FromException<bool>(failure) : Task.FromResult(true));
        }

        var projects = new Mock<IProjectRepository>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        if (!persistenceFails)
        {
            users.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>()))
                .ReturnsAsync(new User { Id = user.Id, EmailAddress = user.EmailAddress });
            projects.Setup(repository => repository.GetByOrganizationIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<Project>>()))
                .Callback(() => calls.Add("cleanup"))
                .ThrowsAsync(failure);
            publisher.Setup(client => client.PublishAsync(typeof(UserMembershipChanged), It.IsAny<object>(), null, default))
                .Callback((Type _, object message, MessageOptions? _, CancellationToken _) =>
                {
                    published.Add((UserMembershipChanged)message);
                    calls.Add("publish");
                })
                .Returns(Task.CompletedTask);
        }

        var context = CreateContext();
        var handler = CreateHandler(organizations.Object, users.Object, projects.Object,
            new Mock<ISavedViewRepository>(MockBehavior.Strict).Object, publisher.Object, context);

        // Act
        var actual = await Record.ExceptionAsync(async () =>
        {
            _ = await handler.Handle(new RemoveOrganizationUser(organization.Id, user.EmailAddress, context));
        });

        // Assert
        if (persistenceFails)
        {
            Assert.Same(failure, actual);
            Assert.Equal(["persist"], calls);
            Assert.Empty(published);
        }
        else
        {
            Assert.Same(failure, actual);
            Assert.Equal(alreadyRemoved ? ["publish", "cleanup"] : ["persist", "publish", "cleanup"], calls);
            var notification = Assert.Single(published);
            Assert.Equal(user.Id, notification.UserId);
            Assert.Equal(organization.Id, notification.OrganizationId);
            Assert.Equal(ChangeType.Removed, notification.ChangeType);
        }

        organizations.VerifyAll();
        users.VerifyAll();
        projects.VerifyAll();
        publisher.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_RemoveOrganizationUserPatchRejected_DoesNotPublishOrCleanUp(bool userDeleted)
    {
        // Arrange
        var organization = new Organization { Id = "000000000000000000000001", Name = "Test" };
        var user = new User { Id = "000000000000000000000002", EmailAddress = "removed@example.test" };
        user.OrganizationIds.Add(organization.Id);
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        organizations.Setup(repository => repository.GetByIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<Organization>>())).ReturnsAsync(organization);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByEmailAddressAsync(user.EmailAddress)).ReturnsAsync(user);
        users.Setup(repository => repository.GetByOrganizationIdAsync(organization.Id, null)).ReturnsAsync(new FindResults<User>([], 2));
        users.Setup(repository => repository.PatchAsync(user.Id, It.IsAny<IPatchOperation>(), It.IsAny<CommandOptionsDescriptor<User>>()))
            .Returns(() => userDeleted ? Task.FromException<bool>(new DocumentNotFoundException("User deleted")) : Task.FromResult(false));
        var context = CreateContext();
        var handler = CreateHandler(organizations.Object, users.Object,
            new Mock<IProjectRepository>(MockBehavior.Strict).Object,
            new Mock<ISavedViewRepository>(MockBehavior.Strict).Object,
            new Mock<IMessagePublisher>(MockBehavior.Strict).Object, context);

        // Act
        var result = await handler.Handle(new RemoveOrganizationUser(organization.Id, user.EmailAddress, context));

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains(organization.Id, user.OrganizationIds);
        users.VerifyAll();
        users.VerifyNoOtherCalls();
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, AuthorizationRoles.GlobalAdmin)], IdentityUtils.UserAuthenticationType))
        };
    }

    private OrganizationHandler CreateHandler(IOrganizationRepository organizations, IUserRepository users, IProjectRepository projects,
        ISavedViewRepository savedViews, IMessagePublisher publisher, HttpContext context, IMailer? mailer = null)
    {
        var usage = GetService<UsageService>();
        var stripe = new FakeStripeBillingClient();
        var service = new OrganizationService(organizations, projects, savedViews, GetService<ITokenRepository>(), users,
            GetService<IWebHookRepository>(), stripe, usage, GetService<ILockProvider>(), Log);
        var billing = new BillingManager(GetService<ILockProvider>(), organizations, projects, users, GetService<BillingPlans>(), TimeProvider);

        return new OrganizationHandler(service, organizations, GetService<ICacheClient>(), GetService<IEventRepository>(), users, projects,
            billing, GetService<BillingPlans>(), usage, stripe, mailer ?? GetService<IMailer>(), publisher, new ApiMapper(TimeProvider),
            GetService<AppOptions>(), TimeProvider, new HttpContextAccessor { HttpContext = context },
            new Mock<LinkGenerator>(MockBehavior.Strict).Object, Log);
    }
}
