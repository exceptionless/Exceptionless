using System.Text;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Foundatio.Queues;
using Foundatio.Storage;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventPostServiceTests : IntegrationTestsBase
{
    private readonly IQueue<EventPost> _eventQueue;
    private readonly EventPostService _eventPostService;
    private readonly IFileStorage _storage;

    public EventPostServiceTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventQueue = GetService<IQueue<EventPost>>();
        _eventPostService = GetService<EventPostService>();
        _storage = GetService<IFileStorage>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await _eventQueue.DeleteQueueAsync();
    }

    [Fact]
    public async Task SaveAndEnqueueAsync_WhenBodyExceedsLimit_DoesNotQueueAndDeletesSavedFiles()
    {
        byte[] payload = Encoding.UTF8.GetBytes("123456");
        await using var stream = new EventPostRequestBodyStream(new MemoryStream(payload), 5);

        var result = await _eventPostService.SaveAndEnqueueAsync(new EventPost(true)
        {
            ApiVersion = 2,
            MediaType = "application/json",
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            UserAgent = "exceptionless-test"
        }, stream, TestCancellationToken);

        Assert.True(result.IsRejected);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, result.RejectedStatusCode);
        Assert.False(result.IsQueued);
        Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
        Assert.Empty(await _storage.GetFileListAsync(cancellationToken: TestCancellationToken));
    }

    [Fact]
    public async Task ProcessingTracking_RetryDescendantsDelayTerminalCompletion()
    {
        const string correlationId = "retry-descendants";
        Assert.True(await _eventPostService.InitializeProcessingTrackingAsync(correlationId, TestConstants.ProjectId));
        var eventPost = new EventPost(false)
        {
            ApiVersion = 2,
            OrganizationId = TestConstants.OrganizationId,
            ProcessingCorrelationId = correlationId,
            ProjectId = TestConstants.ProjectId
        };

        Assert.True(await _eventPostService.AddPendingProcessingUnitsAsync(eventPost, 2));

        await _eventPostService.MarkProcessingCompletedAsync("parent", eventPost);
        Assert.False((await GetStatusAsync(correlationId)).IsCompleted);

        await _eventPostService.MarkProcessingCompletedAsync("child-1", eventPost);
        Assert.False((await GetStatusAsync(correlationId)).IsCompleted);

        // Duplicate delivery of a completed queue unit must not consume another pending slot.
        await _eventPostService.MarkProcessingCompletedAsync("child-1", eventPost);
        Assert.False((await GetStatusAsync(correlationId)).IsCompleted);

        await _eventPostService.MarkProcessingCompletedAsync("child-2", eventPost);
        Assert.True((await GetStatusAsync(correlationId)).IsCompleted);
    }

    private async Task<EventPostProcessingStatus> GetStatusAsync(string correlationId)
    {
        var statuses = await _eventPostService.GetProcessingStatusesAsync([correlationId]);
        return statuses[correlationId];
    }
}
