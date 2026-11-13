using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services.BulkOperations;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class EventBulkSelectionResolverPlanTests : TestWithServices
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private readonly Organization _organization = new() { Id = OrganizationId, HasPremiumFeatures = true };
    private readonly Mock<IEventRepository> _events = new();
    private readonly EventBulkSelectionResolver _resolver;

    public EventBulkSelectionResolverPlanTests(ITestOutputHelper output) : base(output)
    {
        var organizations = new Mock<IOrganizationRepository>();
        organizations.Setup(repository => repository.GetByIdAsync(OrganizationId, It.IsAny<CommandOptionsDescriptor<Organization>>())).ReturnsAsync(_organization);
        _events.Setup(repository => repository.CountAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .ReturnsAsync(new CountResult(1));
        _events.Setup(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .ReturnsAsync(new FindResults<PersistentEvent>());
        var validator = GetService<PersistentEventQueryValidator>();
        var serializer = GetService<ITextSerializer>();
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        // These filters use only the configured event fields, so no server mapping is needed.
        var mapping = new TypeMappingDescriptor<PersistentEvent>();
        configuration.Events.ConfigureIndexMapping(mapping);
        configuration.Events.QueryParser.Configuration!.UseMappings(() => mapping, configuration.Client.Infer);
        _resolver = new EventBulkSelectionResolver(_events.Object, organizations.Object, Mock.Of<IProjectRepository>(), validator, serializer, configuration);
    }

    [Fact]
    public async Task PrepareAsync_PremiumFeaturesRemovedAfterValidation_RejectsBeforeCounting()
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
        _events.Verify(repository => repository.CountAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Once);
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
        Assert.Empty(page.Ids);
        _events.Verify(repository => repository.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()), Times.Once);
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
