using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Mail;

public sealed class MailerTests : TestWithServices
{
    private readonly IMailer _mailer;
    private readonly AppOptions _options;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly OrganizationData _organizationData;
    private readonly ProjectData _projectData;
    private readonly StackData _stackData;
    private readonly EventData _eventData;
    private readonly UserData _userData;

    public MailerTests(ITestOutputHelper output) : base(output)
    {
        _organizationData = GetService<OrganizationData>();
        _projectData = GetService<ProjectData>();
        _stackData = GetService<StackData>();
        _eventData = GetService<EventData>();
        _userData = GetService<UserData>();
        _mailer = GetService<IMailer>();
        _options = GetService<AppOptions>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();

        if (_mailer is NullMailer)
            _mailer = new Mailer(GetService<IQueue<MailMessage>>(), GetService<FormattingPluginManager>(), GetService<ITextSerializer>(), _options, TimeProvider, Log.CreateLogger<Mailer>());
    }

    [Fact]
    public void CanParseSmtpUri()
    {
        var uri = new SmtpUri("smtps://test%40test.com:testpass@smtp.test.com:587");
        Assert.NotNull(uri);
        Assert.True(uri.IsSecure);
        Assert.Equal("smtp.test.com", uri.Host);
        Assert.Equal(587, uri.Port);
        Assert.Equal("test@test.com", uri.User);
        Assert.Equal("testpass", uri.Password);
    }

    [Fact]
    public Task SendEventNoticeSimpleErrorAsync()
    {
        var ex = GetException();
        Assert.NotNull(ex);

        return SendEventNoticeAsync(new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.SimpleError, new SimpleError {
                            Message = ex.Message,
                            Type = ex.GetType().FullName,
                            StackTrace = ex.StackTrace
                        }
                    }
                }
        });
    }

    [Fact]
    public Task SendEventNoticeErrorAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.Error, _eventData.GenerateError()
                    }
                }
        });
    }


    [Fact]
    public Task SendEventNoticeErrorWithDetailsAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Geo = "44.5241,-87.9056",
            ReferenceId = "ex_blake_dreams_of_cookies",
            Tags = new TagSet(new[] { "Out", "Of", "Cookies", "Critical" }),
            Count = 2,
            Value = 500,
            Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Error, _eventData.GenerateError() },
                    { Event.KnownDataKeys.Version, "1.2.3" },
                    { Event.KnownDataKeys.UserInfo, new UserInfo("niemyjski", "Blake Niemyjski")  },
                    { Event.KnownDataKeys.UserDescription, new UserDescription("noreply@exceptionless.io", "Blake ate two boxes of cookies and needs help") }
                }
        });
    }

    [Fact]
    public Task SendEventNoticeNotFoundAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Source = "[GET] /not-found?page=20",
            Type = Event.KnownTypes.NotFound
        });
    }

    [Fact]
    public Task SendEventNoticeFeatureAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Source = "My Feature Usage",
            Value = 1,
            Type = Event.KnownTypes.FeatureUsage
        });
    }

    [Fact]
    public Task SendEventNoticeEmptyLogEventAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Value = 1,
            Type = Event.KnownTypes.Log
        });
    }

    [Fact]
    public Task SendEventNoticeLogMessageAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Message = "Only Message",
            Type = Event.KnownTypes.Log
        });
    }

    [Fact]
    public Task SendEventNoticeLogSourceAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Source = "Only Source",
            Type = Event.KnownTypes.Log
        });
    }

    [Fact]
    public Task SendEventNoticeLogReallyLongSourceAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Source = "Soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooorce",
            Type = Event.KnownTypes.Log
        });
    }

    [Fact]
    public Task SendEventNoticeLogMessageSourceLevelAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Message = "My Message",
            Source = "My Source",
            Type = Event.KnownTypes.Log,
            Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Warn" }
                }
        });
    }

    [Fact]
    public Task SendEventNoticeDefaultAsync()
    {
        return SendEventNoticeAsync(new PersistentEvent
        {
            Message = "Default Test Message",
            Source = "Default Test Source"
        });
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithHostileUserDescription_EncodesHtmlAndMailtoComponents()
    {
        var body = await SendEventNoticeAsync(new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Message = "Hostile user description",
            Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.UserInfo, new UserInfo("user-id", "<img src=x onerror=alert(1)>") },
                    { Event.KnownDataKeys.UserDescription, new UserDescription("victim@example.com", "hello&bcc=attacker@example.com") }
                }
        });

        Assert.Contains("mailto:victim%40example.com?body=hello%26bcc%3Dattacker%40example.com", body, StringComparison.Ordinal);
        Assert.DoesNotContain("&bcc=", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", body, StringComparison.Ordinal);
    }

    private async Task<string> SendEventNoticeAsync(PersistentEvent ev)
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        ev.Id = TestConstants.EventId;
        ev.OrganizationId = TestConstants.OrganizationId;
        ev.ProjectId = TestConstants.ProjectId;
        ev.StackId = TestConstants.StackId;

        await _mailer.SendEventNoticeAsync(user, ev, project, RandomData.GetBool(), RandomData.GetBool(), 1);
        var body = await RunMailJobAsync();
        Assert.Contains("View Event Details", body, StringComparison.Ordinal);
        return body;
    }

    [Fact]
    public async Task SendOrganizationAddedAsync()
    {
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        await _mailer.SendOrganizationAddedAsync(user, organization, user);
        var body = await RunMailJobAsync();
        Assert.Contains("View Organization", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOrganizationInviteAsync()
    {
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        await _mailer.SendOrganizationInviteAsync(user, organization, new Invite
        {
            DateAdded = DateTime.UtcNow,
            EmailAddress = "test@exceptionless.com",
            Token = "1"
        });

        var body = await RunMailJobAsync();
        Assert.Contains("Join Organization", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOrganizationHourlyOverageNoticeAsync()
    {
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        await _mailer.SendOrganizationNoticeAsync(user, organization, false, true);
        var body = await RunMailJobAsync();
        Assert.Contains("throttled", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendOrganizationMonthlyOverageNoticeAsync()
    {
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        await _mailer.SendOrganizationNoticeAsync(user, organization, true, false);
        var body = await RunMailJobAsync();
        Assert.Contains("monthly plan limit", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendOrganizationPaymentFailedAsync()
    {
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        await _mailer.SendOrganizationPaymentFailedAsync(user, organization);
        var body = await RunMailJobAsync();
        Assert.Contains("Payment failed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var mostFrequent = _stackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);

        await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, null, DateTime.UtcNow.Date, true, 12, 1, 0, 1, 0, 0, false);
        var body = await RunMailJobAsync();
        Assert.Contains("View Timeline", body, StringComparison.Ordinal);
        Assert.Contains("Most Frequent", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryWithAllBlockedAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var mostFrequent = _stackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);

        await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, null, DateTime.UtcNow.Date, true, 123456, 1, 0, 1, 123456, 0, false);
        var body = await RunMailJobAsync();
        Assert.Contains("discarded due to throttling", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendProjectDailySummaryNotConfiguredAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        await _mailer.SendProjectDailySummaryAsync(user, project, null, null, DateTime.UtcNow.Date, false, 0, 0, 0, 0, 0, 0, false);
        var body = await RunMailJobAsync();
        Assert.Contains("Configure Project", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryWithNoEventsButHasFixedEventsAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        await _mailer.SendProjectDailySummaryAsync(user, project, null, null, DateTime.UtcNow.Date, true, 0, 0, 0, 10, 0, 0, false);
        var body = await RunMailJobAsync();
        Assert.Contains("marked as fixed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendProjectDailySummaryWithNoEventsButHasFixedAndTooBigEventsAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        await _mailer.SendProjectDailySummaryAsync(user, project, null, null, DateTime.UtcNow.Date, true, 0, 0, 0, 10, 123456, 23, false);
        var body = await RunMailJobAsync();
        Assert.Contains("marked as fixed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendProjectDailySummaryWithFreeProjectAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var mostFrequent = _stackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);
        var newest = _stackData.GenerateStacks(1, generateId: true, type: Event.KnownTypes.Error);

        await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, newest, DateTime.UtcNow.Date, true, 12, 1, 1, 2, 0, 0, true);
        var body = await RunMailJobAsync();
        Assert.Contains("free plan", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendProjectDailySummaryWithRegressedStackAsync()
    {
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var regressedStack = _stackData.GenerateStack(generateId: true, type: Event.KnownTypes.Error, status: StackStatus.Regressed);

        await _mailer.SendProjectDailySummaryAsync(user, project, new[] { regressedStack }, null, DateTime.UtcNow.Date, true, 5, 3, 1, 0, 0, 0, false);
        var body = await RunMailJobAsync();
        Assert.Contains("[REGRESSED]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendUserPasswordResetAsync()
    {
        var user = _userData.GenerateSampleUser();
        user.CreatePasswordResetToken(TimeProvider);

        await _mailer.SendUserPasswordResetAsync(user);
        var body = await RunMailJobAsync();
        Assert.Contains("Reset Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("?cancel=true", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendUserEmailVerifyAsync()
    {
        var user = _userData.GenerateSampleUser();
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

        await _mailer.SendUserEmailVerifyAsync(user);
        var body = await RunMailJobAsync();
        Assert.Contains("Verify Address", body, StringComparison.Ordinal);
    }

    private async Task<string> RunMailJobAsync()
    {
        var job = GetService<MailMessageJob>();
        await job.RunAsync();

        if (GetService<IMailSender>() is not InMemoryMailSender sender)
            return String.Empty;

        var body = sender.LastMessage?.Body ?? String.Empty;

        _logger.LogTrace("To:      {To}", sender.LastMessage?.To);
        _logger.LogTrace("Subject: {Subject}", sender.LastMessage?.Subject);
        _logger.LogTrace("Body:\n{Body}", body);

        Assert.NotEmpty(body);
        Assert.Contains("<!DOCTYPE html", body, StringComparison.OrdinalIgnoreCase);
        // Catch unrendered Handlebars tokens — any {{ remaining means a rendering failure
        Assert.DoesNotContain("{{", body, StringComparison.Ordinal);

        return body;
    }

    private Exception? GetException()
    {
        void TestInner()
        {
            void TestInnerInner()
            {
                throw new ApplicationException("Random Test Exception");
            }

            TestInnerInner();
        }

        try
        {
            TestInner();
        }
        catch (Exception ex)
        {
            return ex;
        }

        return null;
    }
}
