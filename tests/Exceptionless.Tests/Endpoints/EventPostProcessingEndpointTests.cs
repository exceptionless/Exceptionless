using Exceptionless.Core;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Xunit;

namespace Exceptionless.Tests.Endpoints;

public sealed class EventPostProcessingEndpointTests : IntegrationTestsBase
{
    public EventPostProcessingEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        GetService<AppOptions>().EventIngestionV3.EnableProcessingStatus = true;
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task GetStatusesAsync_TrackedPost_ReportsQueuedThenCompleted()
    {
        // An empty post still traverses the V2 queue and reaches a terminal processing state,
        // without making this correlation test depend on event-index creation.
        const string json = "[]";
        using HttpResponseMessage postResponse = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "events")
            .Header(Headers.TrackEventPost, "true")
            .Content(json, "application/json")
            .StatusCodeShouldBeAccepted());

        Assert.True(postResponse.Headers.TryGetValues(Headers.EventPostId, out var values));
        string queueEntryId = Assert.Single(values);

        // Tracking is initialized by the queue worker so the request path performs no
        // benchmark-only cache writes. Initialize it here without running the whole job.
        Assert.True(await GetService<EventPostService>().InitializeProcessingTrackingAsync(queueEntryId, SampleDataService.TEST_PROJECT_ID));

        var queued = await GetStatusesAsync(queueEntryId);
        Assert.Equal(1, queued.Requested);
        Assert.Equal(1, queued.Queued);
        Assert.Equal(0, queued.Completed);
        Assert.Equal(0, queued.Unknown);

        await SendRequestAsync(r => r
            .Post()
            .AsFreeOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "events", "posts", "status")
            .Content(new EventPostProcessingStatusRequest { Ids = [queueEntryId] })
            .StatusCodeShouldBeNotFound());

        await GetService<EventPostService>().MarkProcessingCompletedAsync(queueEntryId, new EventPost(false)
        {
            OrganizationId = TestConstants.OrganizationId,
            ProcessingCorrelationId = queueEntryId,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
        });

        var completed = await GetStatusesAsync(queueEntryId);
        Assert.Equal(1, completed.Completed);
        Assert.Equal(0, completed.Queued);
        Assert.Equal(0, completed.Unknown);
    }

    [Fact]
    public Task GetStatusesAsync_TooManyIdentifiers_ReturnsUnprocessableEntity()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "events", "posts", "status")
            .Content(new EventPostProcessingStatusRequest { Ids = Enumerable.Range(0, 1001).Select(index => $"post-{index}").ToArray() })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public async Task ProcessingStatusDisabled_DoesNotCreatePublicTrackingSurface()
    {
        var options = GetService<AppOptions>().EventIngestionV3;
        options.EnableProcessingStatus = false;
        try
        {
            using HttpResponseMessage postResponse = await SendRequestAsync(request => request
                .Post()
                .AsTestOrganizationClientUser()
                .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "events")
                .Header(Headers.TrackEventPost, "true")
                .Content("[]", "application/json")
                .StatusCodeShouldBeAccepted());

            Assert.False(postResponse.Headers.Contains(Headers.EventPostId));
            await SendRequestAsync(request => request
                .Post()
                .AsTestOrganizationClientUser()
                .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "events", "posts", "status")
                .Content(new EventPostProcessingStatusRequest { Ids = ["unknown"] })
                .StatusCodeShouldBeNotFound());
        }
        finally
        {
            options.EnableProcessingStatus = true;
        }
    }

    private async Task<EventPostProcessingSummary> GetStatusesAsync(string queueEntryId)
    {
        var result = await SendRequestAsAsync<EventPostProcessingSummary>(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "events", "posts", "status")
            .Content(new EventPostProcessingStatusRequest { Ids = [queueEntryId] })
            .StatusCodeShouldBeOk());
        return Assert.IsType<EventPostProcessingSummary>(result);
    }
}
