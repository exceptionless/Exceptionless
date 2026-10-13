using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class ThrottleBotsPluginTests : TestWithServices
{
    private readonly IQueue<WorkItemData> _queue;
    private readonly ThrottleBotsPlugin _plugin;

    public ThrottleBotsPluginTests(ITestOutputHelper output) : base(output)
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 21, 2, 25, 0, TimeSpan.Zero));
        _queue = GetService<IQueue<WorkItemData>>();
        _plugin = GetService<ThrottleBotsPlugin>();
    }

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        options.AppMode = AppMode.Production;
        options.BotThrottleLimit = 2;
        base.RegisterServices(services, options);
    }

    [Theory]
    [InlineData(TestConstants.OrganizationId, TestConstants.ProjectIdWithNoRoles)]
    [InlineData(TestConstants.OrganizationId2, TestConstants.ProjectId)]
    public async Task EventBatchProcessingAsync_SharedIpAcrossScopes_KeepsCountersSeparate(string organizationId, string projectId)
    {
        // Arrange
        var first = CreateContext();
        var other = CreateContext(organizationId, projectId);
        var repeated = CreateContext();

        // Act
        await _plugin.EventBatchProcessingAsync([first]);
        await _plugin.EventBatchProcessingAsync([other]);
        var statsBeforeLimit = await _queue.GetQueueStatsAsync();
        await _plugin.EventBatchProcessingAsync([repeated]);

        // Assert
        Assert.False(first.IsDiscarded);
        Assert.False(other.IsDiscarded);
        Assert.False(other.IsCancelled);
        Assert.Equal(0, statsBeforeLimit.Queued);
        Assert.True(repeated.IsDiscarded);
        Assert.True(repeated.IsCancelled);
        Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Queued);
        var entry = await _queue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(entry);
        var workItem = GetService<ISerializer>().Deserialize<RemoveBotEventsWorkItem>(entry.Value.Data);
        Assert.NotNull(workItem);
        Assert.Equal(TestConstants.OrganizationId, workItem.OrganizationId);
        Assert.Equal(TestConstants.ProjectId, workItem.ProjectId);
        Assert.Equal("203.0.113.10", workItem.ClientIpAddress);
        await entry.CompleteAsync();
    }

    [Fact]
    public async Task EventBatchProcessingAsync_MixedProjects_OnlyDiscardsEnabledProjectAtLimit()
    {
        // Arrange
        var disabled = CreateContext(projectId: TestConstants.SuspendedProjectId, enabled: false);
        var first = CreateContext();
        var second = CreateContext();
        var other = CreateContext(projectId: TestConstants.ProjectIdWithNoRoles);

        // Act
        await _plugin.EventBatchProcessingAsync([disabled, first, second, other]);

        // Assert
        Assert.True(first.IsDiscarded);
        Assert.True(first.IsCancelled);
        Assert.True(second.IsDiscarded);
        Assert.True(second.IsCancelled);
        Assert.False(disabled.IsDiscarded);
        Assert.False(disabled.IsCancelled);
        Assert.False(other.IsDiscarded);
        Assert.False(other.IsCancelled);
        Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Queued);
    }

    [Fact]
    public async Task EventBatchProcessingAsync_NextWindow_StartsNewCounter()
    {
        // Arrange
        await _plugin.EventBatchProcessingAsync([CreateContext(), CreateContext()]);
        var next = CreateContext();

        // Act
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await _plugin.EventBatchProcessingAsync([next]);

        // Assert
        Assert.False(next.IsDiscarded);
        Assert.False(next.IsCancelled);
        Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Queued);
    }

    private static EventContext CreateContext(string organizationId = TestConstants.OrganizationId, string projectId = TestConstants.ProjectId, bool enabled = true)
    {
        var ev = new PersistentEvent { Type = Event.KnownTypes.Error };
        ev.AddRequestInfo(new RequestInfo { ClientIpAddress = "203.0.113.10" });
        return new EventContext(ev, new Organization { Id = organizationId },
            new Project { Id = projectId, OrganizationId = organizationId, DeleteBotDataEnabled = enabled });
    }
}
