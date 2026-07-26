using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class BackfillParentReferencesMigrationTests : IntegrationTestsBase
{
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;

    public BackfillParentReferencesMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<BackfillParentReferences>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_WithRetainedEvents_BackfillsOnlyMissingParentIndexes()
    {
        var missingIndexEvent = _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, stackId: TestConstants.StackId, generateData: false, occurrenceDate: TimeProvider.GetUtcNow());
        missingIndexEvent.Id = ObjectId.GenerateNewId().ToString();
        missingIndexEvent.Data = new() { [$"@ref:{Event.KnownReferenceNames.Parent}"] = "missing-parent-index" };
        missingIndexEvent.Idx = null;

        var existingIndexEvent = _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, stackId: TestConstants.StackId, generateData: false, occurrenceDate: TimeProvider.GetUtcNow());
        existingIndexEvent.Id = ObjectId.GenerateNewId().ToString();
        existingIndexEvent.Data = new() { [$"@ref:{Event.KnownReferenceNames.Parent}"] = "source-parent-reference" };
        existingIndexEvent.Idx = new() { [$"{Event.KnownReferenceNames.Parent}-r"] = "preserved-parent-index" };

        var unrelatedEvent = _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, stackId: TestConstants.StackId, generateData: false, occurrenceDate: TimeProvider.GetUtcNow());
        unrelatedEvent.Id = ObjectId.GenerateNewId().ToString();
        unrelatedEvent.Data = new() { ["custom"] = "value" };
        unrelatedEvent.Idx = null;

        await _eventRepository.AddAsync([missingIndexEvent, existingIndexEvent, unrelatedEvent], options => options.ImmediateConsistency());

        var before = await _eventRepository.FindAsync(query => query.FieldEquals("idx.parent-r", "missing-parent-index"));
        Assert.Empty(before.Documents);

        var migration = GetService<BackfillParentReferences>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);
        await RefreshDataAsync();

        var backfilledEvent = await _eventRepository.GetByIdAsync(missingIndexEvent.Id, options => options.Include(ev => ev.Idx));
        Assert.NotNull(backfilledEvent);
        Assert.NotNull(backfilledEvent.Idx);
        Assert.Equal("missing-parent-index", backfilledEvent.Idx[$"{Event.KnownReferenceNames.Parent}-r"]);

        var preservedEvent = await _eventRepository.GetByIdAsync(existingIndexEvent.Id, options => options.Include(ev => ev.Idx));
        Assert.NotNull(preservedEvent);
        Assert.NotNull(preservedEvent.Idx);
        Assert.Equal("preserved-parent-index", preservedEvent.Idx[$"{Event.KnownReferenceNames.Parent}-r"]);

        var skippedEvent = await _eventRepository.GetByIdAsync(unrelatedEvent.Id, options => options.Include(ev => ev.Idx));
        Assert.NotNull(skippedEvent);
        Assert.Null(skippedEvent.Idx);
    }
}
