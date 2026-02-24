using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class FixStackStatsJobTests : IntegrationTestsBase
{
    private readonly WorkItemJob _workItemJob;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly StackData _stackData;
    private readonly EventData _eventData;

    private static readonly DateTime DefaultWindowStart = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DefaultWindowEnd = new(2026, 2, 23, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InWindowDate = new(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

    public FixStackStatsJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _workItemJob = GetService<WorkItemJob>();
        _workItemQueue = GetService<IQueue<WorkItemData>>();
        _stackRepository = GetService<IStackRepository>();
        _eventRepository = GetService<IEventRepository>();
        _stackData = GetService<StackData>();
        _eventData = GetService<EventData>();
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WhenStackIsInBugWindowWithCorruptCounters_ShouldRebuildStackStatsFromEvents()
    {
        // Arrange
        // Simulate the corrupted state: stack created in bug window with TotalOccurrences = 0
        TimeProvider.SetUtcNow(InWindowDate);

        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true,
                organizationId: TestConstants.OrganizationId,
                projectId: TestConstants.ProjectId,
                utcFirstOccurrence: InWindowDate,
                utcLastOccurrence: InWindowDate,
                totalOccurrences: 0)
            , o => o.ImmediateConsistency());

        // Events exist with known occurrence dates — as if they were posted but the Redis
        // ValueTuple bug caused stack stat increments to be silently dropped.
        var first = new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero);
        var middle = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);
        var last = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
        await _eventRepository.AddAsync(
            [
                _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId,
                    projectId: TestConstants.ProjectId, stackId: stack.Id, occurrenceDate: first),
                _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId,
                    projectId: TestConstants.ProjectId, stackId: stack.Id, occurrenceDate: middle),
                _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId,
                    projectId: TestConstants.ProjectId, stackId: stack.Id, occurrenceDate: last),
            ],
            o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
        {
            UtcStart = DefaultWindowStart,
            UtcEnd = DefaultWindowEnd
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(3, stack.TotalOccurrences);
        Assert.Equal(first.UtcDateTime, stack.FirstOccurrence);
        Assert.Equal(last.UtcDateTime, stack.LastOccurrence);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WhenStackCreatedBeforeBugWindow_ShouldSkipRepair()
    {
        // Arrange
        // Stack created before the window start; its stats should not be touched even if wrong.
        TimeProvider.SetUtcNow(new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc));

        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true,
            organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId,
            totalOccurrences: 0), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(
            [
                _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId,
                    projectId: TestConstants.ProjectId, stackId: stack.Id,
                    occurrenceDate: new DateTimeOffset(2026, 2, 5, 12, 0, 0, TimeSpan.Zero))
            ],
            o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
        {
            UtcStart = DefaultWindowStart, // Feb 10 — after this stack's CreatedUtc (Feb 5)
            UtcEnd = DefaultWindowEnd
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(0, stack.TotalOccurrences); // Not touched — outside window
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WhenStackCreatedAfterBugWindowEnd_ShouldSkipRepair()
    {
        // Arrange
        // Stack created after the window end; its stats should not be touched.
        TimeProvider.SetUtcNow(new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc));

        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true,
            organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId,
            totalOccurrences: 0), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(
            [
                _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId,
                    projectId: TestConstants.ProjectId, stackId: stack.Id,
                    occurrenceDate: new DateTimeOffset(2026, 2, 24, 0, 0, 0, TimeSpan.Zero))
            ],
            o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
        {
            UtcStart = DefaultWindowStart,
            UtcEnd = DefaultWindowEnd // Feb 23 — before this stack's CreatedUtc (Feb 24)
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(0, stack.TotalOccurrences); // Not touched — outside window
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WhenStackHasNoEvents_ShouldLeaveCountersUnchanged()
    {
        // Arrange
        // Stack is in the bug window but has no events — should be left as-is.
        TimeProvider.SetUtcNow(InWindowDate);

        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true,
            organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId,
            totalOccurrences: 0), o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
        {
            UtcStart = DefaultWindowStart,
            UtcEnd = DefaultWindowEnd
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(0, stack.TotalOccurrences); // Not touched — no events to derive stats from
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WhenAggregatedTotalIsLowerThanCurrent_ShouldNotDecreaseTotalOccurrences()
    {
        // Arrange
        TimeProvider.SetUtcNow(InWindowDate);

        var occurrenceDate = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true,
            organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId,
            utcFirstOccurrence: occurrenceDate,
            utcLastOccurrence: occurrenceDate,
            totalOccurrences: 10), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(
            [
                _eventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id,
                    occurrenceDate: new DateTimeOffset(occurrenceDate, TimeSpan.Zero))
            ],
            o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
        {
            UtcStart = DefaultWindowStart,
            UtcEnd = DefaultWindowEnd
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(10, stack.TotalOccurrences);
    }
}
