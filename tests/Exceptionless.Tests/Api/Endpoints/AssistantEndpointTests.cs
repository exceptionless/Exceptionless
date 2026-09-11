using System.Net;
using System.Text.Json;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Assistant;
using FluentRest;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class AssistantEndpointTests : IntegrationTestsBase
{
    public AssistantEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConversationSharingAsync_PreservesExplicitChoicesAcrossDefaultChanges(bool choice)
    {
        var users = GetService<IUserRepository>();
        var user = await users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        var otherUser = await users.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        Assert.NotNull(otherUser);
        var systemSettings = GetService<SystemSettingsService>();
        var sharingService = GetService<AssistantConversationSharingService>();

        Assert.Null(user.AssistantConversationSharingEnabled);
        Assert.Equal(new(false, false, false), await ReadAsync());
        await systemSettings.UpdateAsync(otherUser.Id, settings => settings.AssistantConversationSharingDefaultEnabled = choice);
        Assert.Equal(new(choice, choice, false), await ReadAsync());

        // An explicit choice equal to the current default must survive later default changes.
        Assert.Equal(new(choice, choice, true), await SaveAsync(choice));
        await systemSettings.UpdateAsync(otherUser.Id, settings => settings.AssistantConversationSharingDefaultEnabled = !choice);
        Assert.Equal(new(choice, !choice, true), await ReadAsync());
        Assert.Equal(new(choice, !choice, true), await sharingService.GetAsync(user.Id));

        var savedUser = await users.GetByIdAsync(user.Id);
        Assert.NotNull(savedUser);
        Assert.Equal(choice, savedUser.AssistantConversationSharingEnabled);
        Assert.Equal(user.FullName, savedUser.FullName);
        Assert.Equal(user.EmailAddress, savedUser.EmailAddress);
        Assert.Equal(user.OrganizationIds, savedUser.OrganizationIds);
        Assert.Equal(user.EmailNotificationsEnabled, savedUser.EmailNotificationsEnabled);
        Assert.Null((await users.GetByIdAsync(otherUser.Id))!.AssistantConversationSharingEnabled);

        Assert.Equal(new(!choice, !choice, false), await SaveAsync(null));
        Assert.Null((await users.GetByIdAsync(user.Id))!.AssistantConversationSharingEnabled);
        Assert.Equal(new(!choice, !choice, false), await ReadAsync());

        Task<AssistantConversationSharingSettings?> ReadAsync() => SendRequestAsAsync<AssistantConversationSharingSettings>(request => request
            .AsTestOrganizationUser().AppendPath("assistant/conversation-sharing").StatusCodeShouldBeOk());
        Task<AssistantConversationSharingSettings?> SaveAsync(bool? enabled) => SendRequestAsAsync<AssistantConversationSharingSettings>(request => request
            .Put().AsTestOrganizationUser().AppendPath("assistant/conversation-sharing")
            .Content(new UpdateAssistantConversationSharing { Enabled = enabled }).StatusCodeShouldBeOk());
    }

    [Fact]
    public Task SetConversationSharingAsync_Anonymous_ReturnsUnauthorized() => SendRequestAsync(request => request
        .Put().AsAnonymousUser().AppendPath("assistant/conversation-sharing")
        .Content(new UpdateAssistantConversationSharing { Enabled = true }).StatusCodeShouldBeUnauthorized());

    [Fact]
    public Task GetConversationSharingAsync_Anonymous_ReturnsUnauthorized() => SendRequestAsync(request => request
        .AsAnonymousUser().AppendPath("assistant/conversation-sharing").StatusCodeShouldBeUnauthorized());

    [Fact]
    public Task SetConversationSharingAsync_MissingChoice_ReturnsBadRequest() => SendRequestAsync(request => request
        .Put().AsTestOrganizationUser().AppendPath("assistant/conversation-sharing")
        .Content(new { }).ExpectedStatus(HttpStatusCode.BadRequest));

    [Fact]
    public Task StreamAssistantChatAsync_Anonymous_ReturnsUnauthorized()
    {
        return SendRequestAsync(request => request
            .Post()
            .AsAnonymousUser()
            .AppendPath("assistant/chat")
            .Content(new { messages = new[] { new { role = "user", content = "Hello" } } })
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public Task StreamAssistantChatAsync_Disabled_ReturnsNotFound()
    {
        return SendRequestAsync(request => request
            .Post()
            .BearerToken(SampleDataService.TEST_USER_API_KEY)
            .AppendPath("assistant/chat")
            .Content(new { messages = new[] { new { role = "user", content = "Hello" } } })
            .ExpectedStatus(HttpStatusCode.NotFound));
    }

    [Fact]
    public void MapAccessFailure_NotConfigured_ReturnsServiceUnavailable()
    {
        var decision = AssistantAccessDecision.Unavailable(
            AssistantAccessReason.NotConfigured,
            "Exie is not configured.",
            enabled: false);

        var result = Exceptionless.Web.Api.Endpoints.AssistantEndpoints.MapAccessFailure(decision);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetAssistantAccessAsync_Disabled_ReturnsHidden()
    {
        var response = await SendRequestAsync(request => request
            .BearerToken(SampleDataService.TEST_USER_API_KEY)
            .AppendPath("assistant/access")
            .StatusCodeShouldBeOk());

        using var access = await response.DeserializeAsync<JsonDocument>();
        Assert.NotNull(access);
        Assert.False(access.RootElement.GetProperty("enabled").GetBoolean());
        Assert.False(access.RootElement.GetProperty("has_access").GetBoolean());
        Assert.False(access.RootElement.GetProperty("upgrade_required").GetBoolean());
        Assert.False(access.RootElement.TryGetProperty("minimum_plan_id", out _));
    }
}
