using System.Reflection;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Utility;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public sealed class RemoveBotEventsWorkItemHandlerTests
{
    [Fact]
    public async Task HandleItemAsync_CleanupTask_RestrictsDeletionToOwningProject()
    {
        var repository = DispatchProxy.Create<IEventRepository, RecordingRepository>();
        var handler = new RemoveBotEventsWorkItemHandler(repository, null!, NullLoggerFactory.Instance);
        var item = CreateWorkItem();
        await handler.HandleItemAsync(CreateContext(item));

        var arguments = Assert.IsType<object[]>(((RecordingRepository)repository).DeleteArguments);
        Assert.Equal(item.OrganizationId, arguments[0]);
        Assert.Equal(item.ProjectId, arguments[1]);
        Assert.Equal(item.ClientIpAddress, arguments[2]);
        Assert.Equal(item.UtcStartDate, arguments[3]);
        Assert.Equal(item.UtcEndDate, arguments[4]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task HandleItemAsync_LegacyTaskWithoutProject_RejectsUnscopedDeletion(string? projectId)
    {
        var repository = DispatchProxy.Create<IEventRepository, RecordingRepository>();
        var handler = new RemoveBotEventsWorkItemHandler(repository, null!, NullLoggerFactory.Instance);
        var item = CreateWorkItem() with { ProjectId = projectId! };
        await Assert.ThrowsAnyAsync<ArgumentException>(() => handler.HandleItemAsync(CreateContext(item)));
        Assert.Equal(0, ((RecordingRepository)repository).DeleteCalls);
    }

    private static RemoveBotEventsWorkItem CreateWorkItem() => new()
    {
        OrganizationId = "organization-a",
        ProjectId = "project-a",
        ClientIpAddress = "203.0.113.10",
        UtcStartDate = new DateTime(2026, 9, 21, 2, 25, 0, DateTimeKind.Utc),
        UtcEndDate = new DateTime(2026, 9, 21, 2, 30, 0, DateTimeKind.Utc)
    };

    private static WorkItemContext CreateContext(RemoveBotEventsWorkItem item) =>
        new(item, "test-job", EmptyLock.Empty, TestContext.Current.CancellationToken, static (_, _) => Task.CompletedTask);

    private class RecordingRepository : DispatchProxy
    {
        public object?[]? DeleteArguments { get; private set; }
        public int DeleteCalls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEventRepository.RemoveAllAsync))
            {
                DeleteCalls++;
                DeleteArguments = args;
                return Task.FromResult(1L);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
