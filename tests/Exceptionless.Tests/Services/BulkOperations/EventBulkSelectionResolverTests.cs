using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Core.Utility;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class EventBulkSelectionResolverTests : IntegrationTestsBase
{
    private readonly EventBulkSelectionResolver _resolver;
    private readonly IEventRepository _eventRepository;

    public EventBulkSelectionResolverTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _resolver = GetService<EventBulkSelectionResolver>();
        _eventRepository = GetService<IEventRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task GetPageAsync_MoreEventsThanOnePage_PagesWithTheCursorInDateOrderWithoutGapsOrRepeats()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var task = NewFilterTask("");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 2);

        // Assert
        Assert.Equal(5, preparation.Total);
        Assert.Equal([2, 2, 1], pages.Select(p => p.Count).ToArray());
        Assert.Equal(events.OrderByDescending(e => e.Date).Select(e => e.Id).ToArray(), pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task GetPageAsync_EarlierPagesAreDeletedBetweenPages_ContinuesAfterTheCursorWithoutSkippingAnything()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var task = NewFilterTask("");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);
        var ordered = events.OrderByDescending(e => e.Date).Select(e => e.Id).ToArray();

        // Act
        var first = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 2, TestCancellationToken);
        await _eventRepository.RemoveAsync(events.Where(e => first.Ids.Contains(e.Id)).ToList(), o => o.ImmediateConsistency());
        var second = await _resolver.GetPageAsync(task, preparation.PinnedState, first.NextCursor, 2, TestCancellationToken);
        var third = await _resolver.GetPageAsync(task, preparation.PinnedState, second.NextCursor, 2, TestCancellationToken);

        // Assert
        Assert.Equal(ordered[..2], first.Ids.ToArray());
        Assert.Equal(ordered[2..4], second.Ids.ToArray());
        Assert.Equal(ordered[4..], third.Ids.ToArray());
    }

    [Fact]
    public async Task GetPageAsync_EventsStoredAfterTheCutoff_AreNeverSelected()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, early) = await CreateDataAsync(d => d.Event().TestProject().Message("early").Date(now.AddHours(-1)).CreatedDate(now.UtcDateTime));
        var cutoff = now.UtcDateTime.AddMinutes(1);
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var (_, late) = await CreateDataAsync(d => d.Event().TestProject().Message("late").Date(now.AddHours(-1)).CreatedDate(TimeProvider.GetUtcNow().UtcDateTime));
        var task = NewFilterTask("", createdBefore: cutoff);
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 10);

        // Assert
        Assert.Equal(1, preparation.Total);
        Assert.Equal(early.Select(e => e.Id).ToArray(), pages.SelectMany(p => p).ToArray());
        Assert.DoesNotContain(late.Single().Id, pages.SelectMany(p => p));
    }

    [Fact]
    public async Task GetPageAsync_StacksChangeStatusAfterPrepare_StillPagesTheEventsThatWereConfirmed()
    {
        // Arrange
        var (stacks, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("open").Status(StackStatus.Open);
            d.Event().TestProject().Message("fixed").Status(StackStatus.Fixed);
        });
        var task = NewFilterTask("status:open");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);
        var openStack = stacks.Single(s => s.Status == StackStatus.Open);
        openStack.Status = StackStatus.Fixed;
        await GetService<IStackRepository>().SaveAsync(openStack, o => o.ImmediateConsistency());

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 10);

        // Assert
        Assert.Equal(1, preparation.Total);
        Assert.Equal([events.Single(e => e.Message == "open").Id], pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task GetPageAsync_NothingWasPrepared_ReturnsAnEmptyPageInsteadOfSelectingEverything()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().TestProject());
        var task = NewFilterTask("");

        // Act
        var page = await _resolver.GetPageAsync(task, null, null, 10, TestCancellationToken);

        // Assert
        Assert.Empty(page.Ids);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPageAsync_ProjectScopedTask_OnlySelectsThatProjectsEvents()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("own").Date(now.AddHours(-1));
            d.Event().TestProject().Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID).Message("other project").Date(now.AddHours(-1));
            d.Event().FreeProject().Message("other organization").Date(now.AddHours(-1));
        });
        var task = NewFilterTask("");
        task.ProjectId = SampleDataService.TEST_PROJECT_ID;
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 10);

        // Assert
        Assert.Equal(1, preparation.Total);
        Assert.Equal([events.Single(e => e.Message == "own").Id], pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task PrepareAsync_RelativeDates_ArePinnedToTheCutoff()
    {
        // Arrange
        var task = NewFilterTask("date:[now-2h TO now]");

        // Act
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Assert
        Assert.True(preparation.Check.IsOk);
        Assert.NotNull(preparation.PinnedState);
        Assert.DoesNotContain("now", preparation.PinnedState, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("||-2h", preparation.PinnedState);
    }

    [Fact]
    public async Task GetPageAsync_RelativeDateTerm_UsesTheSelectionCutoffInsteadOfExecutionTime()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-1);
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("confirmed first").Date(cutoff.AddHours(-1))
                .Mutate(e => e.Data = new DataDictionary { ["scheduled"] = cutoff })
                .MutateStack(stack => stack.CreatedUtc = cutoff.AddHours(-2));
            d.Event().TestProject().Message("confirmed second").Date(cutoff.AddHours(-2))
                .Mutate(e => e.Data = new DataDictionary { ["scheduled"] = cutoff.Date.AddHours(12) })
                .MutateStack(stack => stack.CreatedUtc = cutoff.AddHours(-2));
            d.Event().TestProject().Message("different day").Date(cutoff.AddHours(-1))
                .Mutate(e => e.Data = new DataDictionary { ["scheduled"] = now })
                .MutateStack(stack => stack.CreatedUtc = cutoff.AddHours(-2));
        });
        // Use yesterday's selection anchor so the regression does not need to wait for Elasticsearch's clock to advance.
        var task = NewFilterTask("data.scheduled:now/d", createdBefore: cutoff);
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 1);

        // Assert
        Assert.Equal(2, preparation.Total);
        Assert.Equal(events.Where(e => e.Message!.StartsWith("confirmed", StringComparison.Ordinal)).OrderByDescending(e => e.Date).Select(e => e.Id).ToArray(),
            pages.SelectMany(page => page).ToArray());
    }

    [Theory]
    [InlineData(SampleDataService.TEST_ORG_ID, "type:log", BulkOperationCheckStatus.Ok)]
    [InlineData(SampleDataService.TEST_ORG_ID, "type:(log", BulkOperationCheckStatus.Invalid)]
    [InlineData(SampleDataService.FREE_ORG_ID, "tag:premium-tag", BulkOperationCheckStatus.PlanLimit)]
    public async Task ValidateSelectionAsync_Filter_IsCheckedForSyntaxAndPlan(string organizationId, string filter, BulkOperationCheckStatus expected)
    {
        // Arrange
        var organization = await GetService<IOrganizationRepository>().GetByIdAsync(organizationId);
        var selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Filter, Filter = filter };

        // Act
        var result = await _resolver.ValidateSelectionAsync(organization!, selection);

        // Assert
        Assert.Equal(expected, result.Status);
        if (expected is BulkOperationCheckStatus.Invalid)
            Assert.Equal("selection.filter", result.Field);
    }

    private async Task<List<List<string>>> GetAllPagesAsync(BackgroundTask task, string? pinnedState, int size)
    {
        var pages = new List<List<string>>();
        string? cursor = null;
        for (int guard = 0; guard < 20; guard++)
        {
            var page = await _resolver.GetPageAsync(task, pinnedState, cursor, size, TestCancellationToken);
            if (page.Ids.Count == 0)
                return pages;

            pages.Add(page.Ids.ToList());
            cursor = page.NextCursor;
        }

        throw new InvalidOperationException("Paging did not finish.");
    }

    private BackgroundTask NewFilterTask(string filter, DateTime? createdBefore = null)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        return new BackgroundTask
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = "events.delete",
            CreatedUtc = now,
            CreatedByUserId = "537650f3b77efe23a47914f8",
            Bulk = new BulkOperationDetails
            {
                Selection = new BulkOperationSelection
                {
                    Kind = BulkOperationSelectionKind.Filter,
                    Filter = filter,
                    UtcStart = now.AddDays(-7),
                    UtcEnd = now,
                    UtcCreatedBefore = createdBefore ?? now
                }
            }
        };
    }
}
