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
}
