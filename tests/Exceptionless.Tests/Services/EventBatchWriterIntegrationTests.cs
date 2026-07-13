using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventBatchWriterIntegrationTests : IntegrationTestsBase
{
    private readonly IEventBatchWriter _writer;
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;

    public EventBatchWriterIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _writer = GetService<IEventBatchWriter>();
        _eventRepository = GetService<IEventRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task ReconcileAsync_RegressionDuplicate_ReloadsCompleteEventBeforeSave()
    {
        string eventId = ObjectId.GenerateNewId().ToString();
        const string message = "preserve this event payload";
        const string referenceId = "reconcile-regression-ref-0001";
        const string marker = "reconciliation payload survives";
        var data = await CreateDataAsync(builder => builder.Event()
            .TestProject()
            .Id(eventId)
            .Type(Event.KnownTypes.Error)
            .Source("Reconciliation.Test")
            .Message(message)
            .ReferenceId(referenceId)
            .Mutate(ev => ev.Data!["reconciliation_marker"] = marker)
            .MutateStack(stack =>
            {
                stack.Status = StackStatus.Regressed;
                stack.RegressionEventId = eventId;
            }));
        PersistentEvent persistedEvent = Assert.Single(data.Events);
        Stack stack = Assert.Single(data.Stacks);
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(organization);
        Assert.NotNull(project);

        await _writer.ReconcileAsync(
            [new EventIngestionReconciliation(persistedEvent.Id, stack.Id)],
            organization,
            project,
            TestCancellationToken);

        await RefreshDataAsync();
        PersistentEvent? reconciled = await _eventRepository.GetByIdAsync(eventId);
        Assert.NotNull(reconciled);
        Assert.True(reconciled.IsRegression);
        Assert.Equal(message, reconciled.Message);
        Assert.Equal("Reconciliation.Test", reconciled.Source);
        Assert.Equal(referenceId, reconciled.ReferenceId);
        Assert.Equal(marker, reconciled.Data!["reconciliation_marker"]);
        Assert.Equal(SampleDataService.TEST_ORG_ID, reconciled.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, reconciled.ProjectId);
        Assert.Equal(stack.Id, reconciled.StackId);
    }

    [Fact]
    public async Task PrepareAsync_OmittedDateRetryAcrossUtcDay_FindsPersistedEventById()
    {
        DateTimeOffset firstReceipt = new(
            TimeProvider.GetUtcNow().UtcDateTime.Date.AddHours(23).AddMinutes(59).AddSeconds(59),
            TimeSpan.Zero);
        TimeProvider.SetUtcNow(firstReceipt);
        var data = await CreateDataAsync(builder => builder.Event()
            .TestProject()
            .Date(firstReceipt.UtcDateTime));
        Stack stack = Assert.Single(data.Stacks);
        const string clientId = "omitted-date-midnight-retry";
        var source = new EventIngestionV3Event { Id = clientId, Type = Event.KnownTypes.Log };

        EventIngestionIdentity firstIdentity = Assert.Single(await _writer.PrepareAsync(
            [source],
            SampleDataService.TEST_PROJECT_ID,
            firstReceipt.UtcDateTime,
            TestCancellationToken));
        await _eventRepository.AddAsync(new PersistentEvent
        {
            Id = firstIdentity.EventId,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            StackId = stack.Id,
            Type = Event.KnownTypes.Log,
            Source = "EventBatchWriterIntegrationTests",
            Date = firstIdentity.EventDate,
            CreatedUtc = firstIdentity.CreatedUtc,
            ReferenceId = "midnight-retry-event"
        }, o => o.ImmediateConsistency());

        PersistentEvent? persistedById = await _eventRepository.GetByIdAsync(firstIdentity.EventId);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        DateTime retryUtc = TimeProvider.GetUtcNow().UtcDateTime;
        EventIngestionIdentity retryIdentity = Assert.Single(await _writer.PrepareAsync(
            [source],
            SampleDataService.TEST_PROJECT_ID,
            retryUtc,
            TestCancellationToken));

        Assert.NotNull(persistedById);
        Assert.True(retryIdentity.IsDuplicate);
        Assert.True(retryIdentity.IsPersisted);
        Assert.Equal(firstIdentity.EventId, retryIdentity.EventId);
        Assert.Equal(firstIdentity.EventDate, retryIdentity.EventDate);
        Assert.Equal(firstIdentity.CreatedUtc, retryIdentity.CreatedUtc);
        Assert.Equal(firstReceipt, retryIdentity.EventDate);
    }

    [Fact]
    public async Task PrepareAsync_PathologicalFutureDate_UsesReceiptDateWithoutOverflow()
    {
        DateTime utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var source = new EventIngestionV3Event
        {
            Id = "pathological-future-date",
            Type = Event.KnownTypes.Log,
            Date = DateTimeOffset.MaxValue
        };

        EventIngestionIdentity identity = Assert.Single(await _writer.PrepareAsync(
            [source],
            SampleDataService.TEST_PROJECT_ID,
            utcNow,
            TestCancellationToken));

        Assert.Equal(new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)), identity.EventDate);
        Assert.Equal(new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds(), Convert.ToUInt32(identity.EventId[..8], 16));
    }

    [Fact]
    public async Task PrepareAsync_PathologicalPastDate_BoundsDateWithoutOverflow()
    {
        DateTime utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var source = new EventIngestionV3Event
        {
            Id = "pathological-past-date",
            Type = Event.KnownTypes.Log,
            Date = DateTimeOffset.MinValue
        };

        EventIngestionIdentity identity = Assert.Single(await _writer.PrepareAsync(
            [source],
            SampleDataService.TEST_PROJECT_ID,
            utcNow,
            TestCancellationToken));

        Assert.Equal(DateTimeOffset.UnixEpoch, identity.EventDate);
        Assert.Equal(0U, Convert.ToUInt32(identity.EventId[..8], 16));
    }

    [Fact]
    public async Task PrepareAsync_ExplicitDateAndStatusDisabled_DoesNotWriteIdentityMapping()
    {
        const string clientId = "stateless-date-routable-id";
        var options = GetService<AppOptions>().EventIngestionV3;
        bool originalStatusSetting = options.EnableProcessingStatus;
        options.EnableProcessingStatus = false;
        try
        {
            DateTimeOffset eventDate = TimeProvider.GetUtcNow().AddMinutes(-1);
            await _writer.PrepareAsync(
                [new EventIngestionV3Event { Id = clientId, Type = Event.KnownTypes.Log, Date = eventDate }],
                SampleDataService.TEST_PROJECT_ID,
                TimeProvider.GetUtcNow().UtcDateTime,
                TestCancellationToken);

            var mappings = await GetService<IEventIngestionIdStore>().GetAsync(
                SampleDataService.TEST_PROJECT_ID,
                [clientId],
                TestCancellationToken);

            Assert.Empty(mappings);
        }
        finally
        {
            options.EnableProcessingStatus = originalStatusSetting;
        }
    }
}
