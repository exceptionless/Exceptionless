using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class WebHookEndpointTests : IntegrationTestsBase
{
    private readonly IWebHookRepository _webHookRepository;

    public WebHookEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _webHookRepository = GetService<IWebHookRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task PostAsync_CamelCaseBodyMissingSnakeCaseRequiredFields_ReturnsLegacyValidationProblem()
    {
        // Arrange
        string body = JsonSerializer.Serialize(new
        {
            organizationId = SampleDataService.TEST_ORG_ID,
            projectId = SampleDataService.TEST_PROJECT_ID,
            url = "https://example.com/webhook",
            eventTypes = new[] { WebHook.KnownEventTypes.NewError }
        });

        // Act
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(body, "application/json")
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.Equal(["The EventTypes field is required."], problemDetails.Errors["event_types"]);
        Assert.Equal(["The OrganizationId field is required."], problemDetails.Errors["organization_id"]);
        Assert.Equal(["The ProjectId field is required."], problemDetails.Errors["project_id"]);
    }

    [Fact]
    public async Task PostAsync_NewWebHook_MapsAllPropertiesToWebHook()
    {
        // Arrange - Test Mapperly: NewWebHook -> WebHook
        var newWebHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.StackPromoted, WebHook.KnownEventTypes.NewError],
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Url = "https://example.com/webhook"
        };

        // Act
        var webHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(newWebHook)
            .StatusCodeShouldBeCreated()
        );

        // Assert - Verify mapping worked correctly
        Assert.NotNull(webHook);
        Assert.NotNull(webHook.Id);
        Assert.Equal("https://example.com/webhook", webHook.Url);
        Assert.Equal(SampleDataService.TEST_ORG_ID, webHook.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, webHook.ProjectId);
        Assert.Equal(2, webHook.EventTypes.Length);
        Assert.Contains(WebHook.KnownEventTypes.StackPromoted, webHook.EventTypes);
        Assert.Contains(WebHook.KnownEventTypes.NewError, webHook.EventTypes);

        // Verify persisted entity
        var persistedHook = await _webHookRepository.GetByIdAsync(webHook.Id);
        Assert.NotNull(persistedHook);
        Assert.Equal("https://example.com/webhook", persistedHook.Url);
    }

    [Fact]
    public async Task PostAsync_NewWebHook_ReturnsAbsoluteLocation()
    {
        // Arrange
        var webHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.NewError],
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Url = "https://example.com/location-webhook"
        };

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(webHook)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(response.Headers.Location);
        Assert.True(response.Headers.Location.IsAbsoluteUri);
        Assert.Equal("localhost", response.Headers.Location.Host);
        Assert.StartsWith("/api/v2/webhooks/", response.Headers.Location.AbsolutePath);
    }

    [Fact]
    public async Task PostAsync_NewWebHookWithVersion_MapsVersionCorrectly()
    {
        // Arrange - Test that Version is mapped correctly
        var newWebHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.NewError],
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Url = "https://example.com/v2webhook",
            Version = new Version(2, 0)
        };

        // Act
        var webHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(newWebHook)
            .StatusCodeShouldBeCreated()
        );

        // Assert - Version 2.0 should map to "v2"
        Assert.NotNull(webHook);
        Assert.Equal(WebHook.KnownVersions.Version2, webHook.Version);
    }

    [Fact]
    public Task CanCreateNewWebHook()
    {
        return SendRequestAsync(r => r
           .Post()
           .AsTestOrganizationUser()
           .AppendPath("webhooks")
           .Content(new NewWebHook
           {
               EventTypes = [WebHook.KnownEventTypes.StackPromoted],
               OrganizationId = SampleDataService.TEST_ORG_ID,
               ProjectId = SampleDataService.TEST_PROJECT_ID,
               Url = "https://example.com/test"
           })
           .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public async Task PostAsync_WithUnauthorizedOrganization_ReturnsBadRequestProblemDetails()
    {
        // Arrange
        var newWebHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.StackPromoted],
            OrganizationId = SampleDataService.FREE_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Url = "https://example.com/test"
        };

        // Act
        var problemDetails = await SendRequestAsAsync<ProblemDetails>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(newWebHook)
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("Invalid organization id specified.", problemDetails.Title);
    }

    [Fact]
    public async Task PostAsync_WithUnauthorizedProject_ReturnsBadRequestProblemDetails()
    {
        // Arrange
        var newWebHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.StackPromoted],
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.FREE_PROJECT_ID,
            Url = "https://example.com/test"
        };

        // Act
        var problemDetails = await SendRequestAsAsync<ProblemDetails>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(newWebHook)
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("Invalid project id specified.", problemDetails.Title);
    }

    [Fact]
    public async Task PostAsync_WithoutOrganizationOrProject_ReturnsBadRequestProblemDetails()
    {
        // Arrange
        const string email = "webhook-no-organization@exceptionless.test";
        const string password = "Test password";
        const string salt = "1234567890123456";
        var user = new User
        {
            EmailAddress = email,
            FullName = "Web Hook User Without Organization",
            Password = password.ToSaltedHash(salt),
            Salt = salt
        };
        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);
        user.MarkEmailAddressVerified();
        await GetService<IUserRepository>().AddAsync(user, o => o.ImmediateConsistency());

        var newWebHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.StackPromoted],
            Url = "https://example.com/test"
        };

        // Act
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .BasicAuthorization(email, password)
            .AppendPath("webhooks")
            .Content(newWebHook)
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
    }

    [Fact]
    public async Task CreateNewWebHookWithInvalidEventTypeFails()
    {
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
           .Post()
           .AsTestOrganizationUser()
           .AppendPath("webhooks")
           .Content(new NewWebHook
           {
               EventTypes = ["Invalid"],
               OrganizationId = SampleDataService.TEST_ORG_ID,
               ProjectId = SampleDataService.TEST_PROJECT_ID,
               Url = "https://example.com/test"
           })
           .StatusCodeShouldBeUnprocessableEntity()
       );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "event_types"));
    }

    [Fact]
    public async Task SubscribeAsync_UppercaseZapierPrefix_ReturnsNotFound()
    {
        // Arrange
        const string zapierUrl = "https://HOOKS.ZAPIER.COM/hooks/12345";

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe")
            .Content(new Dictionary<string, string>
            {
                { "event", WebHook.KnownEventTypes.StackPromoted },
                { "target_url", zapierUrl }
            })
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var webHooks = await _webHookRepository.GetByUrlAsync(zapierUrl);
        Assert.Empty(webHooks.Documents);
    }

    [Fact]
    public async Task SubscribeWithValidZapierUrlCreatesWebHook()
    {
        const string zapierUrl = "https://hooks.zapier.com/hooks/12345";
        var webhook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe")
            .Content(new Dictionary<string, string>
            {
                { "event", WebHook.KnownEventTypes.StackPromoted },
                { "target_url", zapierUrl }
            })
            .StatusCodeShouldBeCreated());

        Assert.NotNull(webhook);
        Assert.Equal(zapierUrl, webhook.Url);
        Assert.Single(webhook.EventTypes);
        Assert.Contains(WebHook.KnownEventTypes.StackPromoted, webhook.EventTypes);
        Assert.Equal(SampleDataService.TEST_ORG_ID, webhook.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, webhook.ProjectId);
    }

    [Fact]
    public Task SubscribeWithMissingEventReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe")
            .Content(new Dictionary<string, string> { { "target_url", "https://hooks.zapier.com/test" } })
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public Task SubscribeWithMissingUrlReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe")
            .Content(new Dictionary<string, string> { { "event", "stack_promoted" } })
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public Task SubscribeWithNonZapierUrlReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe")
            .Content(new Dictionary<string, string>
            {
                { "event", "stack_promoted" },
                { "target_url", "https://example.com/webhook" }
            })
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task DeleteAsync_ExistingWebHook_ReturnsAccepted()
    {
        // Arrange
        var webHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(new NewWebHook
            {
                EventTypes = [WebHook.KnownEventTypes.NewError],
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = SampleDataService.TEST_PROJECT_ID,
                Url = "https://example.com/delete-test"
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(webHook);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("webhooks", webHook.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Assert
        var deletedHook = await _webHookRepository.GetByIdAsync(webHook.Id);
        Assert.Null(deletedHook);
    }

    [Fact]
    public Task DeleteAsync_NonExistentWebHook_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("webhooks", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetAsync_ExistingWebHook_ReturnsWebHook()
    {
        // Arrange
        var createdHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(new NewWebHook
            {
                EventTypes = [WebHook.KnownEventTypes.StackRegression],
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = SampleDataService.TEST_PROJECT_ID,
                Url = "https://example.com/get-test"
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(createdHook);

        // Act
        var webHook = await SendRequestAsAsync<WebHook>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("webhooks", createdHook.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(webHook);
        Assert.Equal(createdHook.Id, webHook.Id);
        Assert.Equal("https://example.com/get-test", webHook.Url);
        Assert.Equal(SampleDataService.TEST_ORG_ID, webHook.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, webHook.ProjectId);
        Assert.Contains(WebHook.KnownEventTypes.StackRegression, webHook.EventTypes);
    }

    [Fact]
    public Task GetAsync_NonExistentWebHook_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("webhooks", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetByProjectAsync_ExistingProject_ReturnsWebHooks()
    {
        // Arrange
        var createdHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(new NewWebHook
            {
                EventTypes = [WebHook.KnownEventTypes.NewError],
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = SampleDataService.TEST_PROJECT_ID,
                Url = "https://example.com/project-list-test"
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(createdHook);

        await RefreshDataAsync();

        // Act
        var webHooks = await SendRequestAsAsync<List<WebHook>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "webhooks")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(webHooks);
        Assert.NotEmpty(webHooks);
        Assert.Contains(webHooks, h => h.Id == createdHook.Id);
        Assert.All(webHooks, h => Assert.Equal(SampleDataService.TEST_PROJECT_ID, h.ProjectId));
    }

    [Fact]
    public Task GetByProjectAsync_InvalidProjectId_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", "000000000000000000000000", "webhooks")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PostAsync_WithAllEventTypes_CreatesWebHook()
    {
        // Arrange
        var newWebHook = new NewWebHook
        {
            EventTypes = [WebHook.KnownEventTypes.NewError, WebHook.KnownEventTypes.CriticalError, WebHook.KnownEventTypes.StackRegression],
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Url = "https://example.com/all-events"
        };

        // Act
        var webHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("webhooks")
            .Content(newWebHook)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(webHook);
        Assert.Equal(3, webHook.EventTypes.Length);
        Assert.Contains(WebHook.KnownEventTypes.NewError, webHook.EventTypes);
        Assert.Contains(WebHook.KnownEventTypes.CriticalError, webHook.EventTypes);
        Assert.Contains(WebHook.KnownEventTypes.StackRegression, webHook.EventTypes);

        // Verify persisted
        var persistedHook = await _webHookRepository.GetByIdAsync(webHook.Id);
        Assert.NotNull(persistedHook);
        Assert.Equal(3, persistedHook.EventTypes.Length);
    }

    [Fact]
    public async Task Test_WithGetRequest_ReturnsZapierTestMessages()
    {
        // Arrange
        string[] expectedMessages = ["Test message 1.", "Test message 2."];

        // Act
        var messages = await SendRequestAsAsync<IReadOnlyCollection<ZapierTestMessage>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("webhooks", "test")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(messages);
        Assert.Contains(messages, message =>
            message.Id == 1 && String.Equals(message.Message, expectedMessages[0], StringComparison.Ordinal));
        Assert.Contains(messages, message =>
            message.Id == 2 && String.Equals(message.Message, expectedMessages[1], StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://127.0.0.1/path")]
    [InlineData("http://localhost/path")]
    [InlineData("http://169.254.169.254/path")]
    [InlineData("https://user:password@example.com/path")]
    public Task PostAsync_DisallowedDestination_ReturnsBadRequest(string url)
        => SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("webhooks")
            .Content(new NewWebHook { ProjectId = SampleDataService.TEST_PROJECT_ID, Url = url, EventTypes = [WebHook.KnownEventTypes.NewError] })
            .StatusCodeShouldBeBadRequest());

    [Fact]
    public async Task UnsubscribeAsync_ExistingZapierHook_RemovesWebHook()
    {
        // Arrange - create a zapier hook via subscribe
        var logger = Assert.IsType<Foundatio.Xunit.TestLogger>(GetService<ILoggerFactory>());
        const string zapierUrl = "https://hooks.zapier.com/hooks/unsubtest";
        var webHook = await SendRequestAsAsync<WebHook>(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe")
            .Content(new Dictionary<string, string>
            {
                { "event", WebHook.KnownEventTypes.NewError },
                { "target_url", zapierUrl }
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(webHook);

        await RefreshDataAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AppendPath("webhooks/unsubscribe")
            .Content(new Dictionary<string, string> { { "target_url", zapierUrl } })
            .StatusCodeShouldBeOk()
        );

        // Assert
        await RefreshDataAsync();
        var results = await _webHookRepository.GetByUrlAsync(zapierUrl);
        Assert.Empty(results.Documents);
        var unsubscribeLogs = logger.LogEntries.Where(entry => entry.EventId.Name == "RemovingZapierUrls").ToList();
        Assert.NotEmpty(unsubscribeLogs);
        Assert.All(unsubscribeLogs, entry =>
        {
            Assert.DoesNotContain(zapierUrl, entry.Message);
            Assert.DoesNotContain(entry.Properties.Values, value => String.Equals(value?.ToString(), zapierUrl, StringComparison.Ordinal));
        });
    }

    [Theory]
    [InlineData("api/v2/webhooks/subscribe", "[]")]
    [InlineData("api/v2/webhooks/subscribe", "{\"event\":42,\"target_url\":\"https://hooks.zapier.com/hooks/test\"}")]
    [InlineData("api/v2/webhooks/subscribe", "{\"event\":\"NewError\",\"target_url\":false}")]
    [InlineData("api/v1/projecthook/subscribe", "[]")]
    public async Task SubscribeAsync_MalformedShape_ReturnsBadRequest(string path, string json)
    {
        // Act
        var response = await SendRequestAsync(request => request.Post().AsTestOrganizationClientUser()
            .BaseUri(_server.BaseAddress).AppendPath(path).Content(json, "application/json").StatusCodeShouldBeBadRequest());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("api/v2/webhooks/unsubscribe", "[]")]
    [InlineData("api/v2/webhooks/unsubscribe", "{\"target_url\":42}")]
    [InlineData("api/v1/projecthook/unsubscribe", "[]")]
    public async Task UnsubscribeAsync_MalformedShape_ReturnsNotFound(string path, string json)
    {
        // Act
        var response = await SendRequestAsync(request => request.Post().BaseUri(_server.BaseAddress)
            .AppendPath(path).Content(json, "application/json").StatusCodeShouldBeNotFound());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("https://hooks.zapier.com.evil.example/hooks/test")]
    [InlineData("https://hooks.zapier.com@evil.example/hooks/test")]
    [InlineData("https://hooks.zapier.com:444/hooks/test")]
    public async Task SubscribeAsync_LookalikeZapierTarget_ReturnsNotFound(string targetUrl)
    {
        // Arrange
        var subscription = new { @event = WebHook.KnownEventTypes.NewError, target_url = targetUrl };

        // Act
        var response = await SendRequestAsync(request => request.Post().AsTestOrganizationClientUser()
            .AppendPath("webhooks/subscribe").Content(subscription).StatusCodeShouldBeNotFound());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await _webHookRepository.GetByUrlAsync(targetUrl)).Documents);
    }

    [Theory]
    [InlineData("https://hooks.zapier.com.evil.example/hooks/test")]
    [InlineData("https://hooks.zapier.com@evil.example/hooks/test")]
    [InlineData("https://hooks.zapier.com:444/hooks/test")]
    public async Task UnsubscribeAsync_LookalikeZapierTarget_ReturnsNotFound(string targetUrl)
    {
        // Act
        var response = await SendRequestAsync(request => request.Post().AppendPath("webhooks/unsubscribe")
            .Content(new { target_url = targetUrl }).StatusCodeShouldBeNotFound());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public Task UnsubscribeAsync_NonZapierUrl_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Post()
            .AppendPath("webhooks/unsubscribe")
            .Content(new Dictionary<string, string> { { "target_url", "https://example.com/not-zapier" } })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task UnsubscribeAsync_MissingUrl_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Post()
            .AppendPath("webhooks/unsubscribe")
            .Content(new Dictionary<string, string> { { "other_field", "value" } })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task UnsubscribeAsync_UppercaseZapierPrefix_ReturnsNotFound()
    {
        // Arrange
        const string zapierUrl = "https://HOOKS.ZAPIER.COM/hooks/unsubtest";

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AppendPath("webhooks/unsubscribe")
            .Content(new Dictionary<string, string> { { "target_url", zapierUrl } })
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ZapierTestMessage(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("message")] string Message);
}
