using System.Globalization;
using System.Text;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Services.BulkOperations;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Serializer;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class EventBulkSelectionResolverPlanTests : TestWithServices
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private readonly Organization _organization = new() { Id = OrganizationId, HasPremiumFeatures = true };
    private readonly Mock<IEventRepository> _events = new();
    private readonly Mock<ISupportPointInTime> _snapshots;
    private readonly BulkOperationOptions _options = new() { BatchSize = 1 };
    private readonly EventBulkSelectionResolver _resolver;

    public EventBulkSelectionResolverPlanTests(ITestOutputHelper output) : base(output)
    {
        var organizations = new Mock<IOrganizationRepository>();
        organizations.Setup(repository => repository.GetByIdAsync(OrganizationId, It.IsAny<CommandOptionsDescriptor<Organization>>())).ReturnsAsync(_organization);
        _snapshots = _events.As<ISupportPointInTime>();
        _snapshots.Setup(repository => repository.ClosePointInTimeAsync(It.IsAny<string>())).ReturnsAsync(true);
        _events.Setup(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .ReturnsAsync(Results("prepared", 1, false, "selected"));
        var validator = GetService<PersistentEventQueryValidator>();
        var serializer = GetService<ITextSerializer>();
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        // These filters use only the configured event fields, so no server mapping is needed.
        var mapping = new TypeMappingDescriptor<PersistentEvent>();
        configuration.Events.ConfigureIndexMapping(mapping);
        configuration.Events.QueryParser.Configuration!.UseMappings(() => mapping, configuration.Client.Infer);
        var appOptions = GetService<AppOptions>();
        _resolver = new EventBulkSelectionResolver(_events.Object, organizations.Object, Mock.Of<IProjectRepository>(), validator, serializer, configuration, appOptions, _options);
    }

    [Fact]
    public async Task PrepareAsync_PremiumFeaturesRemovedAfterValidation_RejectsBeforeSearching()
    {
        // Arrange
        var task = NewTask("message:selected");

        // Act
        var accepted = await _resolver.ValidateSelectionAsync(_organization, task.Bulk!.Selection);
        _organization.HasPremiumFeatures = false;
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Assert
        Assert.True(accepted.IsOk);
        Assert.Equal(BulkOperationCheckStatus.PlanLimit, preparation.Check.Status);
        Assert.Equal("Please upgrade your plan to use premium search features.", preparation.Check.Message);
        Assert.Null(preparation.PinnedState);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPageAsync_PremiumFeaturesRemovedAfterPreparation_RejectsBeforeSearching()
    {
        // Arrange
        var task = NewTask("message:selected");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);
        _organization.HasPremiumFeatures = false;

        // Act
        var thrown = await Record.ExceptionAsync(async () => await _resolver.GetPageAsync(task, preparation.PinnedState, null, 1, TestCancellationToken));

        // Assert
        Assert.True(preparation.Check.IsOk);
        Assert.Equal(1, preparation.Total);
        var rejection = Assert.IsType<BulkSelectionRejectedException>(thrown);
        Assert.Equal(BulkOperationCheckStatus.PlanLimit, rejection.Check.Status);
        Assert.Equal("Please upgrade your plan to use premium search features.", rejection.Message);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Once);
        _snapshots.Verify(repository => repository.ClosePointInTimeAsync("prepared"), Times.Once);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPageAsync_PremiumFeaturesRemovedForFreeFilter_StillSearches()
    {
        // Arrange
        var task = NewTask("type:log");
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);
        _organization.HasPremiumFeatures = false;

        // Act
        var page = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 1, TestCancellationToken);

        // Assert
        Assert.True(preparation.Check.IsOk);
        Assert.Equal(1, preparation.Total);
        Assert.Equal(["selected"], page.Ids);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"event_filter\":\"type:log\",\"stack_ids\":[\"stack\"]}")]
    public async Task GetPageAsync_MissingOrLegacySelection_FailsWithoutSearchingLiveEvents(string? pinned)
    {
        // Arrange
        var task = NewTask("type:log");

        // Act
        var thrown = await Record.ExceptionAsync(async () => await _resolver.GetPageAsync(task, pinned, null, 1, TestCancellationToken));

        // Assert
        var rejection = Assert.IsType<BulkSelectionRejectedException>(thrown);
        Assert.Contains("start the operation again", rejection.Message);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPageAsync_ExpiredSnapshot_FailsWithoutOpeningAnotherSnapshot()
    {
        // Arrange
        var task = NewTask("type:log");
        _events.SetupSequence(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .ReturnsAsync(Results("prepared", 2, true, "first"))
            // Foundatio returns an empty result without a PIT id for a 404.
            .ReturnsAsync(new FindResults<PersistentEvent>());
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var thrown = await Record.ExceptionAsync(async () => await _resolver.GetPageAsync(task, preparation.PinnedState, null, 1, TestCancellationToken));

        // Assert
        Assert.IsType<BulkSelectionRejectedException>(thrown);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.Is<ICommandOptions>(options => options.GetPointInTimeId() == "prepared")), Times.Once);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Exactly(2));
        _snapshots.Verify(repository => repository.ClosePointInTimeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetPageAsync_RepositoryCapsPageSize_ContinuesWithTheLatestSnapshotAndResumesAfterTheFinalCheckpoint()
    {
        // Arrange
        var task = NewTask("message:selected");
        _events.SetupSequence(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .ReturnsAsync(Results("prepared", 3, true, "first"))
            .ReturnsAsync(Results("renewed", 0, true, "first"))
            .ReturnsAsync(Results("latest", 0, false, "second", "third"));
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        // Simulate a repository returning a full capped page even though the requested size is larger.
        var first = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 20000, TestCancellationToken);
        var last = await _resolver.GetPageAsync(task, preparation.PinnedState, first.NextCursor, 20000, TestCancellationToken);
        _organization.HasPremiumFeatures = false;
        var resumed = await _resolver.GetPageAsync(task, preparation.PinnedState, last.NextCursor, 20000, TestCancellationToken);
        await _resolver.ReleaseAsync(task, preparation.PinnedState, last.NextCursor);

        // Assert
        Assert.Equal(["first"], first.Ids);
        Assert.Equal(["second", "third"], last.Ids);
        Assert.Empty(resumed.Ids);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.Is<ICommandOptions>(options => options.GetPointInTimeId() == "renewed" && !options.ShouldTrackTotalHits())), Times.Once);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Exactly(3));
        _snapshots.Verify(repository => repository.ClosePointInTimeAsync("latest"), Times.Once);
        _snapshots.Verify(repository => repository.ClosePointInTimeAsync("prepared"), Times.Never);
    }

    [Fact]
    public async Task GetPageAsync_SmallSelectionHasAnUnavailablePrefix_ReturnsLaterPinnedIdsAndClosesItsSnapshotImmediately()
    {
        // Arrange
        _options.BatchSize = 3;
        var task = NewTask("type:log");
        _events.SetupSequence(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .ReturnsAsync(Results("prepared", 3, true, "first", "second", "third"))
            .ReturnsAsync(Results(null, 0, false, "third"));
        var preparation = await _resolver.PrepareAsync(task, TestCancellationToken);

        // Act
        var page = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 1, TestCancellationToken);
        var exhausted = await _resolver.GetPageAsync(task, preparation.PinnedState, page.NextCursor, 1, TestCancellationToken);

        // Assert
        Assert.Equal(["third"], page.Ids);
        Assert.Empty(exhausted.Ids);
        _snapshots.Verify(repository => repository.ClosePointInTimeAsync("prepared"), Times.Once);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetPageAsync_RetentionIncreases_KeepsTheInitialBoundsAndCurrentPolicyWithAnInvariantDateAnchor()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 11, 13, 11, 30, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(utcNow);
        _organization.CreatedUtc = utcNow.UtcDateTime.AddDays(-30);
        _organization.RetentionDays = 1;
        var task = NewTask("type:log");
        task.Bulk!.Selection.UtcStart = utcNow.UtcDateTime.AddDays(-7);
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        var filters = new List<string>();
        var retentionPolicies = new List<int>();
        _events.Setup(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .Returns((IRepositoryQuery query, ICommandOptions _) =>
            {
                using var stream = new MemoryStream();
                configuration.Client.RequestResponseSerializer.Serialize(query.GetElasticFilters(), stream);
                filters.Add(Encoding.UTF8.GetString(stream.ToArray()));
                retentionPolicies.Add(query.GetAppFilter()!.Organizations.Single().RetentionDays);
                return Task.FromResult(Results(filters.Count == 1 ? "prepared" : "renewed", 2, true, "selected"));
            });
        var originalCulture = CultureInfo.CurrentCulture;
        BulkSelectionPreparation? preparation = null;
        string? cursor = null;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");

            // Act
            preparation = await _resolver.PrepareAsync(task, TestCancellationToken);
            _organization.RetentionDays = 30;
            TimeProvider.Advance(TimeSpan.FromHours(1));
            var page = await _resolver.GetPageAsync(task, preparation.PinnedState, null, 1, TestCancellationToken);
            cursor = page.NextCursor;

            // Assert
            Assert.Equal(["selected"], page.Ids);
            Assert.Equal(2, filters.Count);
            Assert.Equal(filters[0], filters[1]);
            Assert.Contains("2026-11-13T11:30:00.000Z", filters[0]);
            Assert.DoesNotContain("now", filters[0], StringComparison.OrdinalIgnoreCase);
            Assert.Equal([1, 30], retentionPolicies);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            if (preparation is not null)
                await _resolver.ReleaseAsync(task, preparation.PinnedState, cursor);
        }
    }

    private static FindResults<PersistentEvent> Results(string? pointInTimeId, long total, bool hasMore, params string[] ids)
    {
        var data = new Dictionary<string, object?>();
        if (pointInTimeId is not null)
            data[ElasticDataKeys.PointInTimeId] = pointInTimeId;

        var hits = ids.Select(id => new FindHit<PersistentEvent>(id, null, 0,
            data: new Dictionary<string, object?> { [ElasticDataKeys.Sorts] = new object[] { id } }));
        var results = new FindResults<PersistentEvent>(hits, total, data: data);
        ((IFindResults<PersistentEvent>)results).HasMore = hasMore;
        return results;
    }

    private BackgroundTask NewTask(string filter)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        return new BackgroundTask
        {
            OrganizationId = OrganizationId,
            CreatedUtc = now,
            Bulk = new BulkOperationDetails
            {
                Selection = new BulkOperationSelection
                {
                    Kind = BulkOperationSelectionKind.Filter,
                    Filter = filter,
                    UtcStart = now.AddDays(-1),
                    UtcEnd = now,
                    UtcCreatedBefore = now
                }
            }
        };
    }
}
