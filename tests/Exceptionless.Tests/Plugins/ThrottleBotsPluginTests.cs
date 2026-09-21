using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Serialization;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Serializer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class ThrottleBotsPluginTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 21, 2, 25, 0, TimeSpan.Zero));
    private readonly InMemoryCacheClient _cache;
    private readonly InMemoryQueue<WorkItemData> _queue = new();
    private readonly ThrottleBotsPlugin _plugin;

    public ThrottleBotsPluginTests()
    {
        _cache = new InMemoryCacheClient(new InMemoryCacheClientOptions { TimeProvider = _time });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(AppOptions.BaseURL)] = "http://localhost",
            [nameof(AppOptions.AppMode)] = "Production",
            [nameof(AppOptions.BotThrottleLimit)] = "2"
        }).Build();
        var serializer = new SystemTextJsonSerializer(new JsonSerializerOptions().ConfigureExceptionlessDefaults());
        _plugin = new ThrottleBotsPlugin(_cache, _queue, serializer, _time,
            AppOptions.ReadFromConfiguration(configuration), NullLoggerFactory.Instance);
    }

    [Theory]
    [InlineData("organization-a", "project-b")]
    [InlineData("organization-b", "project-a")]
    public async Task EventBatchProcessingAsync_SharedIpAcrossScopes_KeepsCountersSeparate(string organizationId, string projectId)
    {
        await _plugin.EventBatchProcessingAsync([CreateContext()]);
        var other = CreateContext(organizationId, projectId);
        await _plugin.EventBatchProcessingAsync([other]);
        Assert.False(other.IsDiscarded);
        Assert.Equal(0, (await _queue.GetQueueStatsAsync()).Queued);

        var repeated = CreateContext();
        await _plugin.EventBatchProcessingAsync([repeated]);
        Assert.True(repeated.IsDiscarded);
        Assert.True(repeated.IsCancelled);
        Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Queued);
    }

    [Fact]
    public async Task EventBatchProcessingAsync_MixedProjects_OnlyDiscardsEnabledProjectAtLimit()
    {
        var disabled = CreateContext(projectId: "disabled", enabled: false);
        var first = CreateContext();
        var second = CreateContext();
        var other = CreateContext(projectId: "other");
        await _plugin.EventBatchProcessingAsync([disabled, first, second, other]);
        Assert.True(first.IsDiscarded);
        Assert.True(second.IsDiscarded);
        Assert.False(disabled.IsDiscarded);
        Assert.False(other.IsDiscarded);
        Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Queued);
    }

    [Fact]
    public async Task EventBatchProcessingAsync_NextWindow_StartsNewCounter()
    {
        await _plugin.EventBatchProcessingAsync([CreateContext(), CreateContext()]);
        _time.Advance(TimeSpan.FromMinutes(5));
        var next = CreateContext();
        await _plugin.EventBatchProcessingAsync([next]);
        Assert.False(next.IsDiscarded);
    }

    private static EventContext CreateContext(string organizationId = "organization-a", string projectId = "project-a", bool enabled = true)
    {
        var ev = new PersistentEvent { Type = Event.KnownTypes.Error };
        ev.AddRequestInfo(new RequestInfo { ClientIpAddress = "203.0.113.10" });
        return new EventContext(ev, new Organization { Id = organizationId },
            new Project { Id = projectId, OrganizationId = organizationId, DeleteBotDataEnabled = enabled });
    }

    public void Dispose()
    {
        _queue.Dispose();
        _cache.Dispose();
    }
}
