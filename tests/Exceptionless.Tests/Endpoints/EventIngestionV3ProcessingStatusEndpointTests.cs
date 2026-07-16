using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Endpoints;

public sealed class EventIngestionV3ProcessingStatusEndpointTests : IntegrationTestsBase
{
    public EventIngestionV3ProcessingStatusEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        GetService<AppOptions>().EventIngestionV3.EnableProcessingStatus = true;
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task GetProcessingStatusAsync_CompletedAndPendingEvents_ReturnsAggregate()
    {
        const string completedClientId = "v3-status-completed";
        const string pendingClientId = "v3-status-pending";
        DateTime utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var identity = Assert.Single(await GetService<IEventBatchWriter>().PrepareAsync(
            [new() { Id = completedClientId, Type = Event.KnownTypes.Log }],
            SampleDataService.TEST_PROJECT_ID,
            utcNow,
            TestCancellationToken));
        await GetService<IngestionSideEffectExecutor>().ExecuteAsync(
            IngestionSideEffectExecutor.TerminalStage,
            SampleDataService.TEST_PROJECT_ID,
            [identity.EventId],
            _ => Task.CompletedTask,
            TestCancellationToken);

        var result = await SendRequestAsAsync<EventIngestionV3ProcessingSummary>(request => request
            .Post()
            .AsTestOrganizationClientUser()
            .BaseUri(_server.BaseAddress)
            .AppendPaths("api", "v3", "projects", SampleDataService.TEST_PROJECT_ID, "events", "processing", "status")
            .Content(new EventIngestionV3ProcessingStatusRequest { ClientIds = [completedClientId, pendingClientId] })
            .StatusCodeShouldBeOk());

        var summary = Assert.IsType<EventIngestionV3ProcessingSummary>(result);
        Assert.Equal(2, summary.Requested);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(1, summary.Pending);
    }

    [Fact]
    public Task GetProcessingStatusAsync_TooManyClientIds_ReturnsUnprocessableEntity()
    {
        return SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationClientUser()
            .BaseUri(_server.BaseAddress)
            .AppendPaths("api", "v3", "projects", SampleDataService.TEST_PROJECT_ID, "events", "processing", "status")
            .Content(new EventIngestionV3ProcessingStatusRequest
            {
                ClientIds = Enumerable.Range(0, 1001).Select(index => $"event-{index}").ToArray()
            })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task GetProcessingStatusAsync_StatusDisabled_ReturnsNotFound()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        options.EnableProcessingStatus = false;
        try
        {
            await SendRequestAsync(request => request
                .Post()
                .AsTestOrganizationClientUser()
                .BaseUri(_server.BaseAddress)
                .AppendPaths("api", "v3", "projects", SampleDataService.TEST_PROJECT_ID, "events", "processing", "status")
                .Content(new EventIngestionV3ProcessingStatusRequest { ClientIds = ["event"] })
                .StatusCodeShouldBeNotFound());
        }
        finally
        {
            options.EnableProcessingStatus = true;
        }
    }
}
