using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class ContactHandlerTests : TestWithServices
{
    public ContactHandlerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Handle_ContactEmailAddressNotConfigured_ReturnsServiceUnavailable()
    {
        // Arrange
        var handler = CreateHandler(new EmailOptions(), new RecordingContactMailer());

        // Act
        var result = await handler.Handle(new SubmitContactRequest(CreateRequest(), CreateContext()));

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Handle_MailerReturnsFalse_ReturnsServiceUnavailable()
    {
        // Arrange
        var mailer = new RecordingContactMailer { QueueContactRequests = false };
        var handler = CreateHandler(CreateConfiguredEmailOptions(), mailer);

        // Act
        var result = await handler.Handle(new SubmitContactRequest(CreateRequest(), CreateContext()));

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCodeResult.StatusCode);
        Assert.Single(mailer.ContactRequests);
    }

    private ContactHandler CreateHandler(EmailOptions emailOptions, IMailer mailer)
    {
        return new ContactHandler(
            emailOptions,
            mailer,
            GetService<ICacheClient>(),
            TimeProvider,
            Log.CreateLogger<ContactHandler>());
    }

    private EmailOptions CreateConfiguredEmailOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ContactEmailAddress"] = "contact@example.test"
            })
            .Build();

        return EmailOptions.ReadFromConfiguration(configuration, GetService<AppOptions>());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["User-Agent"] = "Unit Test";
        context.Request.Headers["Referer"] = "http://localhost/contact";
        return context;
    }

    private static ContactRequest CreateRequest()
    {
        return new ContactRequest
        {
            Name = "Test User",
            EmailAddress = "test-user@example.com",
            Message = "This is a valid contact request."
        };
    }

    private sealed class RecordingContactMailer : IMailer
    {
        public List<ContactRequestCall> ContactRequests { get; } = [];
        public bool QueueContactRequests { get; set; } = true;

        public Task<bool> SendContactRequestAsync(string name, string emailAddress, string? company, string? subject, string message, string? clientIpAddress, string? userAgent, string? referrer)
        {
            ContactRequests.Add(new ContactRequestCall(name, emailAddress, company, subject, message, clientIpAddress, userAgent, referrer));
            return Task.FromResult(QueueContactRequests);
        }

        public Task<bool> SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences)
        {
            return Task.FromResult(true);
        }

        public Task SendOrganizationAddedAsync(User sender, Organization organization, User user)
        {
            return Task.CompletedTask;
        }

        public Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite)
        {
            return Task.CompletedTask;
        }

        public Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit)
        {
            return Task.CompletedTask;
        }

        public Task SendOrganizationPaymentFailedAsync(User owner, Organization organization)
        {
            return Task.CompletedTask;
        }

        public Task SendProjectDailySummaryAsync(User user, Project project, IEnumerable<Stack>? mostFrequent, IEnumerable<Stack>? newest, DateTime startDate, bool hasSubmittedEvents, double count, double uniqueCount, double newCount, double fixedCount, int blockedCount, int tooBigCount, bool isFreePlan)
        {
            return Task.CompletedTask;
        }

        public Task SendUserEmailVerifyAsync(User user)
        {
            return Task.CompletedTask;
        }

        public Task SendUserPasswordResetAsync(User user)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record ContactRequestCall(string Name, string EmailAddress, string? Company, string? Subject, string Message, string? ClientIpAddress, string? UserAgent, string? Referrer);
}
