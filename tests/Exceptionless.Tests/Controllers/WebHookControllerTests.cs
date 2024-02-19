using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
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
    public Task CreateNewWebHookWithInvalidEventTypeFails()
    {
        return SendRequestAsync(r => r
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
            .StatusCodeShouldBeBadRequest()
        );
    }
}
