using System.Text.Json;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Assistant;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Foundatio.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantConversationServiceTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public async Task BeginTurnAsync_TurnWithoutToolResults_ConsumesConfirmationButPreservesHistory()
    {
        // Arrange
        using var cache = CreateCache();
        using var messageBus = CreateMessageBus();
        var service = CreateService(cache, messageBus);
        var first = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);
        var pending = CreateConfirmation("first");
        await service.AppendToolResultsAsync("user", "organization", "conversation", first.TurnId!, [pending], TestCancellationToken);

        // Act
        var unanswered = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);
        var next = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);

        // Assert
        Assert.Equal(pending.ToolCallId, unanswered.PendingConfirmation?.ToolCallId);
        Assert.Null(next.PendingConfirmation);
        Assert.Equal(pending.ToolCallId, Assert.Single(next.ToolResults).ToolCallId);
        Assert.False(await service.IsCurrentTurnAsync("user", "organization", "conversation", unanswered.TurnId!));
        Assert.True(await service.IsCurrentTurnAsync("user", "organization", "conversation", next.TurnId!));
    }

    [Fact]
    public async Task BeginTurnAsync_ConcurrentInstances_OnlyOneReceivesThePendingConfirmation()
    {
        // Arrange
        using var cache = CreateCache();
        using var messageBus = CreateMessageBus();
        var firstService = CreateService(cache, messageBus);
        var secondService = CreateService(cache, messageBus);
        var first = await firstService.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);
        await firstService.AppendToolResultsAsync("user", "organization", "conversation", first.TurnId!, [CreateConfirmation("first")], TestCancellationToken);

        // Act
        var turns = await Task.WhenAll(
            firstService.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken),
            secondService.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken));

        // Assert
        Assert.Single(turns, turn => turn.PendingConfirmation is not null);
        Assert.NotEqual(turns[0].TurnId, turns[1].TurnId);
    }

    [Fact]
    public async Task AppendToolResultsAsync_OlderTurnFinishesLate_DoesNotReplaceTheCurrentQuestion()
    {
        // Arrange
        using var cache = CreateCache();
        using var messageBus = CreateMessageBus();
        var service = CreateService(cache, messageBus);
        var earlier = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);
        var current = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);
        await service.AppendToolResultsAsync("user", "organization", "conversation", current.TurnId!, [CreateConfirmation("current")], TestCancellationToken);

        // Act
        await service.AppendToolResultsAsync("user", "organization", "conversation", earlier.TurnId!, [CreateConfirmation("earlier")], TestCancellationToken);
        var next = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);

        // Assert
        Assert.Equal("current", next.PendingConfirmation?.ToolCallId);
        Assert.Equal("current", Assert.Single(next.ToolResults).ToolCallId);
    }

    [Fact]
    public async Task BeginTurnAsync_LegacyToolHistory_DoesNotRestoreAnOldConfirmation()
    {
        // Arrange
        using var cache = CreateCache();
        using var messageBus = CreateMessageBus();
        var service = CreateService(cache, messageBus);
        var scopedCache = new ScopedCacheClient(cache, "AssistantConversation");
        await scopedCache.SetAsync("user:user:organization:organization:conversation:conversation", new AssistantConversationState([CreateConfirmation("legacy")]));

        // Act
        var turn = await service.BeginTurnAsync("user", "organization", "conversation", TestCancellationToken);

        // Assert
        Assert.Null(turn.PendingConfirmation);
        Assert.Equal("legacy", Assert.Single(turn.ToolResults).ToolCallId);
    }

    [Fact]
    public void Serialize_EmptyCollections_RoundTripAsEmpty()
    {
        // Arrange
        // Redis stores state with these options, which omit empty collections.
        var serializer = new SystemTextJsonSerializer(new JsonSerializerOptions().ConfigureExceptionlessDefaults());
        var state = new AssistantConversationState([]) { TurnId = "turn", PendingConfirmation = CreateConfirmation("pending") };

        // Act
        var restored = serializer.Deserialize<AssistantConversationState>(serializer.SerializeToString(state));

        // Assert
        Assert.NotNull(restored);
        Assert.Equal("turn", restored.TurnId);
        Assert.Empty(restored.ToolResults);
        Assert.Equal("pending", restored.PendingConfirmation?.ToolCallId);
        Assert.Empty(restored.PendingConfirmation!.ProjectIds);
    }

    private static AssistantConversationToolResult CreateConfirmation(string id) => new(
        id,
        "delete_saved_view",
        JsonSerializer.Serialize(new { savedViewId = id }),
        AssistantConfirmation.CreateRequiredResult("delete_saved_view", JsonSerializerOptions.Web),
        null,
        DateTimeOffset.UtcNow,
        []);

    private static InMemoryCacheClient CreateCache() => new(new InMemoryCacheClientOptions
    {
        CloneValues = true,
        LoggerFactory = NullLoggerFactory.Instance,
        TimeProvider = TimeProvider.System
    });

    private static InMemoryMessageBus CreateMessageBus() => new(new InMemoryMessageBusOptions
    {
        Serializer = new SystemTextJsonSerializer(new JsonSerializerOptions().ConfigureExceptionlessDefaults()),
        TimeProvider = TimeProvider.System,
        ResiliencePolicyProvider = new ResiliencePolicyProvider(),
        LoggerFactory = NullLoggerFactory.Instance
    });

    private static AssistantConversationService CreateService(ICacheClient cache, IMessageBus messageBus) => new(
        cache,
        new CacheLockProvider(cache, messageBus, TimeProvider.System, new ResiliencePolicyProvider(), NullLoggerFactory.Instance),
        NullLogger<AssistantConversationService>.Instance);
}
