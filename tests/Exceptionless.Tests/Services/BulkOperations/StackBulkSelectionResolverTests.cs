using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Core.Utility;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class StackBulkSelectionResolverTests : IntegrationTestsBase
{
    private readonly StackBulkSelectionResolver _resolver;

    public StackBulkSelectionResolverTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _resolver = GetService<StackBulkSelectionResolver>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task GetPageAsync_MoreStacksThanOnePage_PagesEveryStackOnceInIdOrder()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (stacks, _) = await CreateDataAsync(d =>
        {
            // The first stack has several events, which must not make it appear more than once.
            d.Event().TestProject().Date(now.AddHours(-1)).Create(2);
            for (int i = 0; i < 4; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var task = NewFilterTask("");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 2);

        // Assert
        Assert.Equal(5, preparation.Total);
        Assert.Equal([2, 2, 1], pages.Select(p => p.Count).ToArray());
        Assert.Equal(stacks.Select(s => s.Id).Order(StringComparer.Ordinal).ToArray(), pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task GetPageAsync_EarlierStacksAreDeletedBetweenPages_ContinuesAfterTheCursorWithoutSkippingAnything()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (stacks, _) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var ordered = stacks.Select(s => s.Id).Order(StringComparer.Ordinal).ToArray();
        var task = NewFilterTask("");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var first = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 2, TestCancellationToken);
        var stackRepository = GetService<IStackRepository>();
        await stackRepository.RemoveAsync(stacks.Where(s => first.Ids.Contains(s.Id)).ToList(), o => o.ImmediateConsistency());
        var second = await _resolver.GetPageAsync(task, preparation.PinnedState, first.NextCursor, 2, TestCancellationToken);
        var third = await _resolver.GetPageAsync(task, preparation.PinnedState, second.NextCursor, 2, TestCancellationToken);

        // Assert
        Assert.Equal(ordered[..2], first.Ids.ToArray());
        Assert.Equal(ordered[2..4], second.Ids.ToArray());
        Assert.Equal(ordered[4..], third.Ids.ToArray());
    }

    [Fact]
    public async Task GetPageAsync_LastPageIsNotFull_HasNoCursor()
    {
        // Arrange
        await CreateDataAsync(d =>
        {
            d.Event().TestProject();
            d.Event().TestProject();
        });
        var task = NewFilterTask("");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var page = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 10, TestCancellationToken);

        // Assert
        Assert.Equal(2, page.Ids.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPageAsync_StackLevelFilter_OnlySelectsStacksMatchingItAndKeepsThemAfterTheyChange()
    {
        // Arrange
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("open").Status(StackStatus.Open);
            d.Event().TestProject().Message("fixed").Status(StackStatus.Fixed);
        });
        var task = NewFilterTask("status:open");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);
        var openStack = stacks.Single(s => s.Status is StackStatus.Open);
        openStack.Status = StackStatus.Fixed;
        await GetService<IStackRepository>().SaveAsync(openStack, o => o.ImmediateConsistency());

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 10);

        // Assert
        Assert.Equal(1, preparation.Total);
        Assert.Equal([openStack.Id], pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task GetPageAsync_EventsOutsideTheWindowOrStoredAfterTheCutoff_DoNotSelectTheirStack()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (inside, _) = await CreateDataAsync(d => d.Event().TestProject().Date(now.AddHours(-1)).CreatedDate(now.UtcDateTime));
        await CreateDataAsync(d => d.Event().TestProject().Date(now.AddDays(-20)).CreatedDate(now.UtcDateTime));
        var cutoff = now.UtcDateTime.AddMinutes(1);
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await CreateDataAsync(d => d.Event().TestProject().Date(now.AddHours(-1)).CreatedDate(TimeProvider.GetUtcNow().UtcDateTime));
        var task = NewFilterTask("", createdBefore: cutoff);
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 10);

        // Assert
        Assert.Equal(1, preparation.Total);
        Assert.Equal([inside.Single().Id], pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task GetPageAsync_ProjectScopedTask_OnlySelectsThatProjectsStacks()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Date(now.AddHours(-1));
            d.Event().TestProject().Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID).Date(now.AddHours(-1));
            d.Event().FreeProject().Date(now.AddHours(-1));
        });
        var task = NewFilterTask("");
        task.ProjectId = SampleDataService.TEST_PROJECT_ID;
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var pages = await GetAllPagesAsync(task, preparation.PinnedState, size: 10);

        // Assert
        Assert.Equal(1, preparation.Total);
        Assert.Equal([stacks.Single(s => s.ProjectId is SampleDataService.TEST_PROJECT_ID).Id], pages.SelectMany(p => p).ToArray());
    }

    [Fact]
    public async Task PrepareAsync_PremiumFeaturesRemovedAfterValidation_RejectsInsteadOfSelectingNothing()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().TestProject().Tag("premium-tag"));
        var task = NewFilterTask("tag:premium-tag");
        var organizations = GetService<IOrganizationRepository>();
        var organization = await organizations.GetByIdAsync(SampleDataService.TEST_ORG_ID, o => o.Cache(false));
        organization!.HasPremiumFeatures = false;
        await organizations.SaveAsync(organization, o => o.ImmediateConsistency());

        // Act
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Assert
        Assert.Equal(BulkOperationCheckStatus.PlanLimit, preparation.Check.Status);
    }

    [Theory]
    [InlineData(SampleDataService.TEST_ORG_ID, "status:open", BulkOperationCheckStatus.Ok)]
    [InlineData(SampleDataService.TEST_ORG_ID, "status:(open", BulkOperationCheckStatus.Invalid)]
    [InlineData(SampleDataService.FREE_ORG_ID, "tag:premium-tag", BulkOperationCheckStatus.PlanLimit)]
    public async Task ValidateSelectionAsync_Filter_IsCheckedForSyntaxAndPlan(string organizationId, string filter, BulkOperationCheckStatus expected)
    {
        // Arrange
        var organizations = GetService<IOrganizationRepository>();
        var organization = await organizations.GetByIdAsync(organizationId);
        var selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Filter, Filter = filter };

        // Act
        var result = await _resolver.ValidateSelectionAsync(organization!, selection);

        // Assert
        Assert.Equal(expected, result.Status);
    }

    private async Task<List<List<string>>> GetAllPagesAsync(BackgroundTask task, string? pinnedState, int size)
    {
        var pages = new List<List<string>>();
        string? cursor = null;
        for (int guard = 0; guard < 20; guard++)
        {
            var page = await _resolver.GetPageAsync(task, pinnedState, cursor, size, TestCancellationToken);
            if (page.Ids.Count is 0)
                return pages;

            pages.Add(page.Ids.ToList());
            if (page.NextCursor is null)
                return pages;

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
            Type = "stacks.delete",
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
