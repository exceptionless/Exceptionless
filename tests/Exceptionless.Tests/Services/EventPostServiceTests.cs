using System.Reflection;
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
    public async Task SaveAndEnqueueAsync_WhenQueueReturnsNoEntry_ReturnsFailedAndDeletesSavedFiles()
    {
        // Arrange
        var service = new EventPostService(FailingEnqueueQueue.Create(_eventQueue, exception: null), _storage, TimeProvider, Log);

        // Act
        var result = await service.SaveAndEnqueueAsync(CreateEventPost(), new MemoryStream("{}"u8.ToArray()), TestCancellationToken);

        // Assert
        Assert.False(result.IsQueued);
        Assert.False(result.IsRejected);
        Assert.Empty(await _storage.GetFileListAsync(cancellationToken: TestCancellationToken));
    }

    [Fact]
    public async Task SaveAndEnqueueAsync_WhenQueueThrows_ReturnsFailedAndDeletesSavedFiles()
    {
        // Arrange
        var service = new EventPostService(FailingEnqueueQueue.Create(_eventQueue, new InvalidOperationException("Queue unavailable")), _storage, TimeProvider, Log);

        // Act
        var result = await service.SaveAndEnqueueAsync(CreateEventPost(), new MemoryStream("{}"u8.ToArray()), TestCancellationToken);

        // Assert
        Assert.False(result.IsQueued);
        Assert.False(result.IsRejected);
        Assert.Empty(await _storage.GetFileListAsync(cancellationToken: TestCancellationToken));
    }

    private static EventPost CreateEventPost()
    {
        return new EventPost(false)
        {
            ApiVersion = 2,
            MediaType = "application/json",
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            UserAgent = "exceptionless-test"
        };
    }

    /// <summary>Delegates to a real queue except that enqueuing fails.</summary>
    public class FailingEnqueueQueue : DispatchProxy
    {
        private IQueue<EventPost> _inner = null!;
        private Exception? _exception;

        public static IQueue<EventPost> Create(IQueue<EventPost> inner, Exception? exception)
        {
            var queue = Create<IQueue<EventPost>, FailingEnqueueQueue>();
            var proxy = (FailingEnqueueQueue)(object)queue;
            proxy._inner = inner;
            proxy._exception = exception;
            return queue;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IQueue<EventPost>.EnqueueAsync))
            {
                return _exception is null ? Task.FromResult<string?>(null) : Task.FromException<string?>(_exception);
            }

            return targetMethod.Invoke(_inner, args);
        }
    }
}
