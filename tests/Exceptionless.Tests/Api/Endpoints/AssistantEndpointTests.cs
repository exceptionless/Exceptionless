using System.Net;
using System.Text.Json;
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
    }
}
