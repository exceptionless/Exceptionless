using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
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
    public async Task WillBackfillRetainedParentReference()
    {
        var ev = _eventData.GenerateEvent(organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, stackId: TestConstants.StackId, generateData: false, occurrenceDate: TimeProvider.GetUtcNow());
        ev.Data = new() { [$"@ref:{Event.KnownReferenceNames.Parent}"] = "parent-reference" };
        ev.Idx = null;
        await _eventRepository.AddAsync(ev, options => options.ImmediateConsistency());

        var before = await _eventRepository.FindAsync(query => query.FieldEquals("idx.parent-r", "parent-reference"));
        Assert.Empty(before.Documents);

        var migration = GetService<BackfillParentReferences>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);
        await RefreshDataAsync();

        var after = await _eventRepository.FindAsync(query => query.FieldEquals("idx.parent-r", "parent-reference"));
        Assert.Equal(ev.Id, Assert.Single(after.Documents).Id);
    }
}
