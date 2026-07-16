using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ContactEndpointTests : IntegrationTestsBase
{
    public ContactEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    private RecordingContactMailer Mailer => GetService<RecordingContactMailer>();
    private JsonSerializerOptions JsonOptions => GetService<JsonSerializerOptions>();

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<RecordingContactMailer>();
        services.ReplaceSingleton<IMailer>(sp => sp.GetRequiredService<RecordingContactMailer>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        Mailer.Reset();
    }

    [Fact]
    public async Task PostFormAsync_WithValidRequest_ReturnsAcceptedAndSendsContactRequest()
    {
        // Arrange
        using var client = CreateHttpClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Form User",
            ["EmailAddress"] = "form-user@example.com",
            ["Company"] = "Form Company",
            ["Subject"] = "Form question",
            ["Message"] = "This is a valid form contact request."
        });

        // Act
        var response = await client.PostAsync("contact", content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var call = Assert.Single(Mailer.ContactRequests);
        Assert.Equal("Form User", call.Name);
        Assert.Equal("form-user@example.com", call.EmailAddress);
        Assert.Equal("Form Company", call.Company);
        Assert.Equal("Form question", call.Subject);
    }

    [Fact]
    public async Task PostJsonAsync_WithValidRequest_ReturnsAcceptedAndSendsContactRequest()
    {
        // Arrange
        using var client = CreateHttpClient();

        // Act
        var response = await client.PostAsJsonAsync("contact", new ContactRequest
        {
            Name = "Ada Lovelace",
            EmailAddress = "ada@example.com",
            Company = "Analytical Engines",
            Subject = "Self hosted question",
            Message = "Can you help us understand self hosted deployment options?"
        }, JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var call = Assert.Single(Mailer.ContactRequests);
        Assert.Equal("Ada Lovelace", call.Name);
        Assert.Equal("ada@example.com", call.EmailAddress);
        Assert.Equal("Analytical Engines", call.Company);
        Assert.Equal("Self hosted question", call.Subject);
    }

    [Fact]
    public async Task PostJsonAsync_WithHoneypot_ReturnsAcceptedWithoutSendingContactRequest()
    {
        // Arrange
        using var client = CreateHttpClient();

        // Act
        var response = await client.PostAsJsonAsync("contact", new ContactRequest
        {
            Name = "Spam Bot",
            EmailAddress = "spam@example.com",
            Message = "This message should be ignored by the honeypot field.",
            Website = "https://spam.example"
        }, JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(Mailer.ContactRequests);
    }

    [Fact]
    public async Task PostJsonAsync_WithInvalidEmailAddress_ReturnsUnprocessableEntity()
    {
        // Arrange
        using var client = CreateHttpClient();

        // Act
        var response = await client.PostAsJsonAsync("contact", new ContactRequest
        {
            Name = "Ada Lovelace",
            EmailAddress = "not an email address",
            Message = "This message has enough characters."
        }, JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(Mailer.ContactRequests);
    }

    [Fact]
    public async Task PostJsonAsync_WithTooManyRequests_ReturnsTooManyRequests()
    {
        // Arrange
        using var client = CreateHttpClient();

        // Act
        for (int i = 0; i < 3; i++)
        {
            var allowedResponse = await client.PostAsJsonAsync("contact", CreateValidRequest(i), JsonOptions, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, allowedResponse.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("contact", CreateValidRequest(4), JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal(3, Mailer.ContactRequests.Count);
    }

    private static ContactRequest CreateValidRequest(int index)
    {
        return new ContactRequest
        {
            Name = $"Person {index}",
            EmailAddress = $"person{index}@example.com",
            Subject = "Question",
            Message = "This is a contact request with enough characters."
        };
    }

    private sealed class RecordingContactMailer : IMailer
    {
        public List<ContactRequestCall> ContactRequests { get; } = [];

        public Task<bool> SendContactRequestAsync(string name, string emailAddress, string? company, string? subject, string message, string? clientIpAddress, string? userAgent, string? referrer)
        {
            ContactRequests.Add(new ContactRequestCall(name, emailAddress, company, subject, message, clientIpAddress, userAgent, referrer));
            return Task.FromResult(true);
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

        public void Reset()
        {
            ContactRequests.Clear();
        }
    }

    private sealed record ContactRequestCall(string Name, string EmailAddress, string? Company, string? Subject, string Message, string? ClientIpAddress, string? UserAgent, string? Referrer);
}
