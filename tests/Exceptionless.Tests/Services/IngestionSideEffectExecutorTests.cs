using Exceptionless.Core.Services;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class IngestionSideEffectExecutorTests : TestWithServices
{
    private const string ProjectId = "project";
    private readonly IngestionSideEffectExecutor _executor;

    public IngestionSideEffectExecutorTests(ITestOutputHelper output) : base(output)
    {
        _executor = GetService<IngestionSideEffectExecutor>();
    }

    [Fact]
    public async Task ExecuteAsync_ActionFails_RetryRunsAction()
    {
        string identity = Guid.NewGuid().ToString("N");
        int attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _executor.ExecuteAsync(
            IngestionSideEffectExecutor.StatisticsStage,
            ProjectId,
            [identity],
            _ =>
            {
                attempts++;
                throw new InvalidOperationException("failed");
            },
            TestCancellationToken));

        int completed = await _executor.ExecuteAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity], _ =>
        {
            attempts++;
            return Task.CompletedTask;
        }, TestCancellationToken);

        Assert.Equal(2, attempts);
        Assert.Equal(1, completed);
    }

    [Fact]
    public async Task ExecuteAsync_ActionCompleted_RetryDoesNotRunAction()
    {
        string identity = Guid.NewGuid().ToString("N");
        int attempts = 0;

        int firstCompleted = await _executor.ExecuteAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity], _ =>
        {
            attempts++;
            return Task.CompletedTask;
        }, TestCancellationToken);
        int retryCompleted = await _executor.ExecuteAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity], _ =>
        {
            attempts++;
            return Task.CompletedTask;
        }, TestCancellationToken);

        Assert.Equal(1, attempts);
        Assert.Equal(1, firstCompleted);
        Assert.Equal(0, retryCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentCalls_RunsActionOnce()
    {
        string identity = Guid.NewGuid().ToString("N");
        int attempts = 0;

        Task<int> first = _executor.ExecuteAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity], async _ =>
        {
            Interlocked.Increment(ref attempts);
            await Task.Delay(25, TestCancellationToken);
        }, TestCancellationToken);
        Task<int> second = _executor.ExecuteAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity], async _ =>
        {
            Interlocked.Increment(ref attempts);
            await Task.Delay(25, TestCancellationToken);
        }, TestCancellationToken);

        int[] completed = await Task.WhenAll(first, second);

        Assert.Equal(1, attempts);
        Assert.Equal(1, completed.Sum());
    }

    [Fact]
    public async Task GetCompletedIdentitiesAsync_ReturnsOnlyCompletedMarkers()
    {
        string completedIdentity = Guid.NewGuid().ToString("N");
        string pendingIdentity = Guid.NewGuid().ToString("N");
        await _executor.ExecuteAsync(
            IngestionSideEffectExecutor.TerminalStage,
            ProjectId,
            [completedIdentity],
            _ => Task.CompletedTask,
            TestCancellationToken);

        var completed = await _executor.GetCompletedIdentitiesAsync(
            IngestionSideEffectExecutor.TerminalStage,
            ProjectId,
            [completedIdentity, pendingIdentity]);

        Assert.Equal([completedIdentity], completed);
    }

    [Fact]
    public async Task ExecuteAsync_StatisticsAndNotifications_StoresSharedStageState()
    {
        string identity = Guid.NewGuid().ToString("N");

        await _executor.ExecuteAsync(
            IngestionSideEffectExecutor.StatisticsStage,
            ProjectId,
            [identity],
            _ => Task.CompletedTask,
            TestCancellationToken);
        await _executor.ExecuteAsync(
            IngestionSideEffectExecutor.TerminalStage,
            ProjectId,
            [identity],
            _ => Task.CompletedTask,
            TestCancellationToken);

        var state = await GetService<ICacheClient>().GetAsync<int>(IngestionStackUsageStore.GetStateKey(ProjectId, identity));
        var statistics = await _executor.GetCompletedIdentitiesAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity]);
        var notifications = await _executor.GetCompletedIdentitiesAsync(IngestionSideEffectExecutor.TerminalStage, ProjectId, [identity]);

        Assert.True(state.HasValue);
        Assert.Equal(3, state.Value);
        Assert.Contains(identity, statistics);
        Assert.Contains(identity, notifications);
    }

    [Fact]
    public async Task ExecuteAsync_NotificationsFail_StatisticsCompletionIsPreserved()
    {
        string identity = Guid.NewGuid().ToString("N");
        await _executor.ExecuteAsync(
            IngestionSideEffectExecutor.StatisticsStage,
            ProjectId,
            [identity],
            _ => Task.CompletedTask,
            TestCancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _executor.ExecuteAsync(
            IngestionSideEffectExecutor.TerminalStage,
            ProjectId,
            [identity],
            _ => throw new InvalidOperationException("failed"),
            TestCancellationToken));

        var statistics = await _executor.GetCompletedIdentitiesAsync(IngestionSideEffectExecutor.StatisticsStage, ProjectId, [identity]);
        var notifications = await _executor.GetCompletedIdentitiesAsync(IngestionSideEffectExecutor.TerminalStage, ProjectId, [identity]);
        Assert.Contains(identity, statistics);
        Assert.DoesNotContain(identity, notifications);

        int completed = await _executor.ExecuteAsync(
            IngestionSideEffectExecutor.TerminalStage,
            ProjectId,
            [identity],
            _ => Task.CompletedTask,
            TestCancellationToken);

        Assert.Equal(1, completed);
    }
}
