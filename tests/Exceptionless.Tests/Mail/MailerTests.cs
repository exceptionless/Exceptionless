using System.Net;
using System.Text.RegularExpressions;
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
    private static readonly HashSet<string> _expectedExternalHosts = new(StringComparer.OrdinalIgnoreCase) {
        "exceptionless.com",
        "github.com",
        "www.facebook.com",
        "twitter.com"
    };

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
    public void Constructor_WithSecureSmtpUri_ParsesComponents()
    {
        // Arrange
        const string value = "smtps://test%40test.com:testpass@smtp.test.com:587";

        // Act
        var uri = new SmtpUri(value);

        // Assert
        Assert.NotNull(uri);
        Assert.True(uri.IsSecure);
        Assert.Equal("smtp.test.com", uri.Host);
        Assert.Equal(587, uri.Port);
        Assert.Equal("test@test.com", uri.User);
        Assert.Equal("testpass", uri.Password);
    }

    [Fact]
    public async Task SendContactRequestAsync_WithCompleteRequest_RendersAllFields()
    {
        // Arrange
        const string message = "First line\nSecond line";

        // Act
        bool queued = await _mailer.SendContactRequestAsync(
            "Test User",
            "test@example.com",
            "Example Company",
            "Need help",
            message,
            "127.0.0.1",
            "Test Browser",
            "https://example.com/contact");
        string body = await RunMailJobAsync(requireUrls: false);

        // Assert
        Assert.True(queued);
        Assert.Contains("Test User", body, StringComparison.Ordinal);
        Assert.Contains("Example Company", body, StringComparison.Ordinal);
        Assert.Contains("First line", body, StringComparison.Ordinal);
        Assert.Contains("Second line", body, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", body, StringComparison.Ordinal);
        Assert.Contains("Test Browser", body, StringComparison.Ordinal);
        Assert.Contains("https://example.com/contact", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithSimpleError_RendersEventNotice()
    {
        // Arrange
        var exception = GetException();
        var ev = new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.SimpleError, new SimpleError {
                            Message = exception.Message,
                            Type = exception.GetType().FullName,
                            StackTrace = exception.StackTrace
                        }
                    }
                }
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains(exception.Message, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithStructuredError_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.Error, _eventData.GenerateError()
                    }
                }
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("Generated exception message.", body, StringComparison.Ordinal);
    }


    [Fact]
    public async Task SendEventNoticeAsync_WithDetailedError_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
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
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("Blake Niemyjski", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithNotFoundEvent_RendersSourceUrl()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Source = "[GET] /not-found?page=20",
            Type = Event.KnownTypes.NotFound
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("[GET] /not-found?page=20", WebUtility.HtmlDecode(body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithFeatureEvent_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Source = "My Feature Usage",
            Value = 1,
            Type = Event.KnownTypes.FeatureUsage
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("My Feature Usage", body, StringComparison.Ordinal);
    }

    [Fact]
    public Task SendEventNoticeAsync_WithEmptyLogEvent_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Value = 1,
            Type = Event.KnownTypes.Log
        };

        // Act
        return SendEventNoticeAsync(ev);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithLogMessage_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Message = "Only Message",
            Type = Event.KnownTypes.Log
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("Only Message", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithLogSource_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Source = "Only Source",
            Type = Event.KnownTypes.Log
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("Only Source", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithLongLogSource_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Source = "Soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooorce",
            Type = Event.KnownTypes.Log
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("Soooooooo", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithLogDetails_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Message = "My Message",
            Source = "My Source",
            Type = Event.KnownTypes.Log,
            Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Warn" }
                }
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("My Message", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithDefaultEvent_RendersEventNotice()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Message = "Default Test Message",
            Source = "Default Test Source"
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        Assert.Contains("Default Test Message", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEventNoticeAsync_WithHostileUserDescription_EncodesHtmlAndMailtoComponents()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Type = Event.KnownTypes.Error,
            Message = "Hostile user description",
            Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.UserInfo, new UserInfo("user-id", "<img src=x onerror=alert(1)>") },
                    { Event.KnownDataKeys.UserDescription, new UserDescription("victim@example.com", "hello&bcc=attacker@example.com") }
                }
        };

        // Act
        string body = await SendEventNoticeAsync(ev);

        // Assert
        AssertContainsUrl(body, "mailto:victim%40example.com?body=hello%26bcc%3Dattacker%40example.com");
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
        Assert.Contains($"{_options.BaseURL}/event/{TestConstants.EventId}", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/stack/{TestConstants.StackId}/mark-fixed", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/stack/{TestConstants.StackId}/ignored", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/stack/{TestConstants.StackId}/discarded", body, StringComparison.Ordinal);
        AssertContainsUrl(body, $"{_options.BaseURL}/account/manage?projectId={TestConstants.ProjectId}&tab=notifications");
        return body;
    }

    [Fact]
    public async Task SendOrganizationAddedAsync_WithOrganization_RendersOrganizationLink()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        // Act
        await _mailer.SendOrganizationAddedAsync(user, organization, user);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("View Organization", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/dashboard", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOrganizationInviteAsync_WithInvite_RendersSignupLink()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        var invite = new Invite
        {
            DateAdded = DateTime.UtcNow,
            EmailAddress = "test@exceptionless.com",
            Token = "1"
        };

        // Act
        await _mailer.SendOrganizationInviteAsync(user, organization, invite);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("Join Organization", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/signup?token={invite.Token}", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOrganizationNoticeAsync_WithHourlyOverage_RendersUsageLinks()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        // Act
        await _mailer.SendOrganizationNoticeAsync(user, organization, false, true);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("throttled", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/upgrade", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/frequent", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/manage", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/account/manage?tab=notifications", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOrganizationNoticeAsync_WithMonthlyOverage_RendersUsageLinks()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        // Act
        await _mailer.SendOrganizationNoticeAsync(user, organization, true, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("monthly plan limit", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/upgrade", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/frequent", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/manage", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/account/manage?tab=notifications", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOrganizationPaymentFailedAsync_WithOrganization_RendersBillingLinks()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);

        // Act
        await _mailer.SendOrganizationPaymentFailedAsync(user, organization);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("Payment failed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/organization/{organization.Id}/manage?tab=billing", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithSubmittedEvents_RendersTimelineLinks()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var mostFrequent = _stackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error).ToArray();
        for (int index = 0; index < mostFrequent.Length; index++)
            mostFrequent[index].Id = $"frequent-stack-{index}";

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, null, DateTime.UtcNow.Date, true, 12, 1, 0, 1, 0, 0, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("View Timeline", body, StringComparison.Ordinal);
        Assert.Contains("Most Frequent", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/project/{project.Id}/error/timeline", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/project/{project.Id}/error/frequent", body, StringComparison.Ordinal);
        AssertContainsUrl(body, $"{_options.BaseURL}/account/manage?projectId={project.Id}&tab=notifications");
        Assert.All(mostFrequent, stack => AssertContainsUrl(body, $"{_options.BaseURL}/stack/{stack.Id}"));
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithAllEventsBlocked_RendersThrottleContent()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var mostFrequent = _stackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error);

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, null, DateTime.UtcNow.Date, true, 123456, 1, 0, 1, 123456, 0, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("discarded due to throttling", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/organization/{project.OrganizationId}/upgrade", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithUnconfiguredProject_RendersConfigureLink()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, null, null, DateTime.UtcNow.Date, false, 0, 0, 0, 0, 0, 0, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("Configure Project", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/project/{project.Id}/configure", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithOnlyFixedEvents_RendersFixedContent()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, null, null, DateTime.UtcNow.Date, true, 0, 0, 0, 10, 0, 0, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("marked as fixed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/project/{project.Id}/error/timeline", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithFixedAndOversizedEvents_RendersFixedContent()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, null, null, DateTime.UtcNow.Date, true, 0, 0, 0, 10, 123456, 23, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("marked as fixed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/project/{project.Id}/error/timeline", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithFreeProject_RendersPlanContent()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var mostFrequent = _stackData.GenerateStacks(3, generateId: true, type: Event.KnownTypes.Error).ToArray();
        var newest = _stackData.GenerateStacks(1, generateId: true, type: Event.KnownTypes.Error).ToArray();
        for (int index = 0; index < mostFrequent.Length; index++)
            mostFrequent[index].Id = $"frequent-stack-{index}";
        newest[0].Id = "newest-stack-0";

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, newest, DateTime.UtcNow.Date, true, 12, 1, 1, 2, 0, 0, true);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("free plan", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/organization/{project.OrganizationId}/upgrade", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/project/{project.Id}/error/new", body, StringComparison.Ordinal);
        Assert.All(mostFrequent.Concat(newest), stack => AssertContainsUrl(body, $"{_options.BaseURL}/stack/{stack.Id}"));
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithRegressedStack_RendersRegressedLabel()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var regressedStack = _stackData.GenerateStack(generateId: true, type: Event.KnownTypes.Error, status: StackStatus.Regressed);
        regressedStack.Id = "regressed-stack";

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, new[] { regressedStack }, null, DateTime.UtcNow.Date, true, 5, 3, 1, 0, 0, 0, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("[REGRESSED]", body, StringComparison.Ordinal);
        AssertContainsUrl(body, $"{_options.BaseURL}/stack/{regressedStack.Id}");
    }

    [Fact]
    public async Task SendProjectDailySummaryAsync_WithNonRegressedStack_OmitsRegressedLabel()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        var project = _projectData.GenerateSampleProject();
        var openStack = _stackData.GenerateStack(generateId: true, type: Event.KnownTypes.Error, status: StackStatus.Open);
        openStack.Id = "open-stack";

        // Act
        await _mailer.SendProjectDailySummaryAsync(user, project, new[] { openStack }, null, DateTime.UtcNow.Date, true, 5, 3, 1, 0, 0, 0, false);
        var body = await RunMailJobAsync();

        // Assert
        Assert.DoesNotContain("[REGRESSED]", body, StringComparison.Ordinal);
        AssertContainsUrl(body, $"{_options.BaseURL}/stack/{openStack.Id}");
    }

    [Fact]
    public async Task SendUserPasswordResetAsync_WithResetToken_RendersResetLinks()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        user.CreatePasswordResetToken(TimeProvider);

        // Act
        await _mailer.SendUserPasswordResetAsync(user);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("Reset Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("?cancel=true", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("click here to cancel the password reset request", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{_options.BaseURL}/reset-password/{user.PasswordResetToken}", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendUserEmailVerifyAsync_WithVerificationToken_RendersVerificationLink()
    {
        // Arrange
        var user = _userData.GenerateSampleUser();
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

        // Act
        await _mailer.SendUserEmailVerifyAsync(user);
        var body = await RunMailJobAsync();

        // Assert
        Assert.Contains("Verify Address", body, StringComparison.Ordinal);
        Assert.Contains($"{_options.BaseURL}/account/verify?token={user.VerifyEmailAddressToken}", body, StringComparison.Ordinal);
    }

    private async Task<string> RunMailJobAsync(bool requireUrls = true)
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
        Assert.DoesNotContain("{{", body, StringComparison.Ordinal);
        AssertValidUrls(body, requireUrls);

        return body;
    }

    private void AssertValidUrls(string body, bool requireUrls)
    {
        var urls = GetUrls(body);
        if (!requireUrls)
        {
            Assert.Empty(urls);
            return;
        }

        Assert.NotEmpty(urls);

        var baseUri = new Uri(_options.BaseURL);
        foreach (string url in urls)
        {
            if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Matches(@"^mailto:[^?]+(?:\?body=.+)?$", url);
                continue;
            }

            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri), $"Expected an absolute email URL but found '{url}'.");
            Assert.Contains(uri.Scheme, new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps });

            if (!String.Equals(uri.Authority, baseUri.Authority, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains(uri.Authority, _expectedExternalHosts);
                continue;
            }

            Assert.DoesNotContain("/next/", uri.PathAndQuery, StringComparison.OrdinalIgnoreCase);
            Assert.Matches(@"^/(?:event/[^/?#]+|stack/[^/?#]+(?:/(?:mark-fixed|ignored|discarded))?|project/[^/]+/(?:configure|error/(?:timeline|frequent|new))|account/(?:manage|verify)|organization/[^/]+/(?:dashboard|upgrade|frequent|manage)|signup|reset-password/[^/?#]+)(?:[/?].*)?$", uri.PathAndQuery);
        }
    }

    private static void AssertContainsUrl(string body, string expectedUrl)
    {
        Assert.Contains(expectedUrl, GetUrls(body));
    }

    private static string[] GetUrls(string body)
    {
        string decodedBody = WebUtility.HtmlDecode(body);
        return Regex.Matches(decodedBody, "(?:href=|\\\"(?:target|url)\\\":\\s*)\\\"(?<url>[^\\\"]+)\\\"")
            .Select(match => match.Groups["url"].Value)
            .ToArray();
    }

    private Exception GetException()
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
            throw new InvalidOperationException("Expected exception was not thrown.");
        }
        catch (ApplicationException ex)
        {
            return ex;
        }
    }
}
