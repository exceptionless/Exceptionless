using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers;

public sealed class WebHookControllerTests : IntegrationTestsBase
{
    public WebHookControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
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
               Url = "https://localhost/test"
           })
           .StatusCodeShouldBeCreated()
        );
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
               Url = "https://localhost/test"
           })
           .StatusCodeShouldBeUnprocessableEntity()
       );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "event_types[0]"));
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
}
