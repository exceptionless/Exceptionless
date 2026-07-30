using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using BulkIndexByScrollFailure = Elastic.Clients.Elasticsearch.BulkIndexByScrollFailure;
using DeleteByQueryResponse = Elastic.Clients.Elasticsearch.DeleteByQueryResponse;
using ElasticsearchClient = Elastic.Clients.Elasticsearch.ElasticsearchClient;
using ElasticsearchClientSettings = Elastic.Clients.Elasticsearch.ElasticsearchClientSettings;
using ErrorCause = Elastic.Clients.Elasticsearch.ErrorCause;
using SearchResponse = Elastic.Clients.Elasticsearch.SearchResponse<Exceptionless.Core.Models.PersistentEvent>;
using ShardStatistics = Elastic.Clients.Elasticsearch.ShardStatistics;

namespace Exceptionless.Tests.Jobs;

public class CleanupOrphanedDataJobTests : IntegrationTestsBase
{
    private static readonly TimeSpan RecentIngestWindow = TimeSpan.FromDays(3);
    private readonly CleanupOrphanedDataJob _job;
    private readonly OrganizationData _organizationData;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ProjectData _projectData;
    private readonly IProjectRepository _projectRepository;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public CleanupOrphanedDataJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<CleanupOrphanedDataJob>();
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    private CleanupOrphanedDataJob CreateJob()
    {
        return new CleanupOrphanedDataJob(
            GetService<ExceptionlessElasticConfiguration>(),
            _stackRepository,
            _projectRepository,
            _organizationRepository,
            _eventRepository,
            GetService<ICacheClient>(),
            GetService<ILockProvider>(),
            TimeProvider,
            GetService<IResiliencePolicyProvider>(),
            Log);
    }

    private async Task AssertOnlyEventsRemainAsync(IReadOnlyCollection<PersistentEvent> retainedEvents, IReadOnlyCollection<PersistentEvent> deletedEvents)
    {
        var results = await _eventRepository.GetAllAsync(o => o
            .IncludeSoftDeletes()
            .ImmediateConsistency()
            .PageLimit(retainedEvents.Count + deletedEvents.Count));
        var remainingIds = results.Documents.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(retainedEvents, e => Assert.Contains(e.Id, remainingIds));
        Assert.All(deletedEvents, e => Assert.DoesNotContain(e.Id, remainingIds));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithValidStack_DoesNotDeleteEvents()
    {
        // Arrange - Two tenants, each with valid stacks and events
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(100, organization1.Id, project1.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(100, organization2.Id, project2.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByStackAsync(new JobContext(TestCancellationToken));

        // Assert - All events should remain (no orphans)
        var totalCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(200, totalCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithMixedOrphanedAndValid_OnlyDeletesOrphaned()
    {
        // Arrange - Tenant 1 has valid events; Tenant 2 has orphaned events (stack doesn't exist)
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        // Valid events for both tenants
        var validEvents1 = _eventData.GenerateEvents(50, organization1.Id, project1.Id, stack1.Id).ToList();
        var validEvents2 = _eventData.GenerateEvents(50, organization2.Id, project2.Id, stack2.Id).ToList();

        // Orphaned events (stack IDs that don't exist) in both tenants
        string fakeStackId1 = ObjectId.GenerateNewId().ToString();
        string fakeStackId2 = ObjectId.GenerateNewId().ToString();
        var orphanedEvents1 = _eventData.GenerateEvents(30, organization1.Id, project1.Id, fakeStackId1).ToList();
        var orphanedEvents2 = _eventData.GenerateEvents(20, organization2.Id, project2.Id, fakeStackId2).ToList();

        await _eventRepository.AddAsync(validEvents1.Concat(validEvents2).Concat(orphanedEvents1).Concat(orphanedEvents2), o => o.ImmediateConsistency());

        var totalBefore = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(150, totalBefore);

        // Act
        Log.Reset();
        await CreateJob().DeleteOrphanedEventsByStackAsync(new JobContext(TestCancellationToken));

        // Assert - Only valid events remain
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
        await AssertOnlyEventsRemainAsync(
            validEvents1.Concat(validEvents2).ToList(),
            orphanedEvents1.Concat(orphanedEvents2).ToList());
        Assert.Contains(Log.LogEntries, entry =>
            entry.Message.StartsWith("Deleted 50 recent events referencing missing stacks out of 4 distinct stack IDs since", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_LargeVolume_PreservesAllValidEvents()
    {
        // Arrange - Large volume across two tenants: 5000 valid + 10000 orphaned
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization.Id);
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization.Id, projectId: project.Id);
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        // 5000 valid events for existing stack
        await _eventRepository.AddAsync(_eventData.GenerateEvents(5000, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // 10000 orphaned events with many different fake stack IDs
        var orphanedEvents = _eventData.GenerateEvents(10000, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var totalBefore = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(15000, totalBefore);

        // Act
        await _job.DeleteOrphanedEventsByStackAsync(new JobContext(TestCancellationToken));

        // Assert - Only the 5000 valid events remain
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(5000, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_MultipleValidStacks_PreservesAll()
    {
        // Arrange - Multiple valid stacks in two organizations, no orphans
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        // Multiple stacks per project
        var stacks = new List<Stack>();
        for (int i = 0; i < 10; i++)
        {
            stacks.Add(_stackData.GenerateStack(generateId: true, organizationId: organization1.Id, projectId: project1.Id));
            stacks.Add(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id));
        }
        await _stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());

        // Events across all stacks
        var events = new List<PersistentEvent>();
        foreach (var stack in stacks)
            events.AddRange(_eventData.GenerateEvents(10, stack.OrganizationId, stack.ProjectId, stack.Id));
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByStackAsync(new JobContext(TestCancellationToken));

        // Assert - All 200 events preserved
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(200, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_OnlyOrphanedEventsInOneTenant_OtherTenantUnaffected()
    {
        // Arrange - Tenant 1 has all orphaned events (will be deleted); Tenant 2 has all valid events
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        // Tenant 2 has a valid stack
        var validStack = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync(validStack, o => o.ImmediateConsistency());

        // Tenant 1 events are all orphaned (fake stack IDs)
        var orphanedEvents = _eventData.GenerateEvents(100, organization1.Id, project1.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());

        // Tenant 2 events are all valid
        var validEvents = _eventData.GenerateEvents(100, organization2.Id, project2.Id, validStack.Id).ToList();

        await _eventRepository.AddAsync(orphanedEvents.Concat(validEvents), o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByStackAsync(new JobContext(TestCancellationToken));

        // Assert - Only tenant 2's events remain
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithValidProjects_DoesNotDeleteEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(50, organization.Id, project1.Id, stack1.Id)
            .Concat(_eventData.GenerateEvents(50, organization.Id, project2.Id, stack2.Id)).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByProjectAsync(new JobContext(TestCancellationToken));

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithOrphanedProject_DeletesEventsForMissingProject()
    {
        // Arrange - Events reference a project that doesn't exist
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var validProject = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(validProject, o => o.ImmediateConsistency());

        var validStack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: validProject.Id);
        await _stackRepository.AddAsync(validStack, o => o.ImmediateConsistency());

        // Valid events for existing project
        var validEvents = _eventData.GenerateEvents(75, organization1.Id, validProject.Id, validStack.Id).ToList();

        // Orphaned events referencing a non-existent project in organization 2
        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        string fakeStackId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(50, organization2.Id, fakeProjectId, fakeStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedEvents), o => o.ImmediateConsistency());

        // Act
        Log.Reset();
        await CreateJob().DeleteOrphanedEventsByProjectAsync(new JobContext(TestCancellationToken));

        // Assert - Orphaned events deleted, valid events preserved
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(75, totalAfter);
        await AssertOnlyEventsRemainAsync(validEvents, orphanedEvents);
        Assert.Contains(Log.LogEntries, entry =>
            entry.Message.StartsWith("Deleted 50 recent events referencing missing projects out of 2 distinct project IDs since", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_MultiTenant_EachTenantIndependent()
    {
        // Arrange - Tenant 1 has valid project, Tenant 2 has orphaned project
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        // Only Tenant 1 has a real project
        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        await _stackRepository.AddAsync(stack1, o => o.ImmediateConsistency());

        // Tenant 1 valid events
        var validEvents = _eventData.GenerateEvents(60, organization1.Id, project1.Id, stack1.Id).ToList();

        // Tenant 2 orphaned events (project doesn't exist)
        string nonExistentProjectId = ObjectId.GenerateNewId().ToString();
        string fakeStackId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(40, organization2.Id, nonExistentProjectId, fakeStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedEvents), o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByProjectAsync(new JobContext(TestCancellationToken));

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(60, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithValidOrganizations_DoesNotDeleteEvents()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(80, organization1.Id, project1.Id, stack1.Id)
            .Concat(_eventData.GenerateEvents(80, organization2.Id, project2.Id, stack2.Id)).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByOrganizationAsync(new JobContext(TestCancellationToken));

        // Assert - All events preserved (both organizations exist)
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(160, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithOrphanedOrganization_DeletesEventsForMissingOrganization()
    {
        // Arrange - Valid organization1 + events referencing non-existent organization
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var project = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project.Id);
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        // Valid events
        var validEvents = _eventData.GenerateEvents(100, organization1.Id, project.Id, stack.Id).ToList();

        // Orphaned events referencing a non-existent organization
        string fakeOrganizationId = ObjectId.GenerateNewId().ToString();
        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        string fakeStackId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(50, fakeOrganizationId, fakeProjectId, fakeStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedEvents), o => o.ImmediateConsistency());

        // Act
        Log.Reset();
        await CreateJob().DeleteOrphanedEventsByOrganizationAsync(new JobContext(TestCancellationToken));

        // Assert - Only valid events survive
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
        await AssertOnlyEventsRemainAsync(validEvents, orphanedEvents);
        Assert.Contains(Log.LogEntries, entry =>
            entry.Message.StartsWith("Deleted 50 recent events referencing missing organizations out of 2 distinct organization IDs since", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_TwoTenantsOneDeleted_OnlyDeletesOrphanedTenantEvents()
    {
        // Arrange - Organization 1 exists, Organization 2 does NOT exist (never created) but has events
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        await _stackRepository.AddAsync(stack1, o => o.ImmediateConsistency());

        // Tenant 1 valid events
        var validEvents = _eventData.GenerateEvents(120, organization1.Id, project1.Id, stack1.Id).ToList();

        // Tenant 2 events (organization doesn't exist, simulates post-hard-delete orphans)
        string ghostOrganizationId = ObjectId.GenerateNewId().ToString();
        string ghostProjectId = ObjectId.GenerateNewId().ToString();
        string ghostStackId = ObjectId.GenerateNewId().ToString();
        var ghostEvents = _eventData.GenerateEvents(80, ghostOrganizationId, ghostProjectId, ghostStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(ghostEvents), o => o.ImmediateConsistency());

        // Act
        await _job.DeleteOrphanedEventsByOrganizationAsync(new JobContext(TestCancellationToken));

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(120, totalAfter);
    }

    [Fact]
    public async Task RunAsync_OrphanedEventsAcrossOccurrenceDates_DeletesOnlyEventsWithinCreatedUtcLookback()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);

        var organization = await _organizationRepository.AddAsync(
            _organizationData.GenerateSampleOrganization(_billingManager, _plans),
            o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(
            _projectData.GenerateSampleProject(),
            o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(
            _stackData.GenerateSampleStack(),
            o => o.ImmediateConsistency());

        var cutoffUtc = utcNow.UtcDateTime.Subtract(RecentIngestWindow);
        var beforeCutoffUtc = cutoffUtc.AddMilliseconds(-1);
        var validEvent = _eventData.GenerateEvent(organization.Id, project.Id, stack.Id, occurrenceDate: utcNow);

        string missingStackId = ObjectId.GenerateNewId().ToString();
        var stackOrphanAtCutoff = _eventData.GenerateEvent(organization.Id, project.Id, missingStackId, occurrenceDate: utcNow);
        stackOrphanAtCutoff.CreatedUtc = cutoffUtc;
        var stackOrphanBeforeCutoff = _eventData.GenerateEvent(organization.Id, project.Id, missingStackId, occurrenceDate: utcNow);
        stackOrphanBeforeCutoff.CreatedUtc = beforeCutoffUtc;
        var stackOrphanWithOldOccurrence = _eventData.GenerateEvent(organization.Id, project.Id, missingStackId, occurrenceDate: utcNow.Subtract(TimeSpan.FromDays(30)));
        stackOrphanWithOldOccurrence.CreatedUtc = utcNow.UtcDateTime;

        string missingProjectId = ObjectId.GenerateNewId().ToString();
        var projectOrphanAtCutoff = _eventData.GenerateEvent(organization.Id, missingProjectId, stack.Id, occurrenceDate: utcNow);
        projectOrphanAtCutoff.CreatedUtc = cutoffUtc;
        var secondProjectOrphanAtCutoff = _eventData.GenerateEvent(organization.Id, ObjectId.GenerateNewId().ToString(), stack.Id, occurrenceDate: utcNow);
        secondProjectOrphanAtCutoff.CreatedUtc = cutoffUtc;
        var projectOrphanBeforeCutoff = _eventData.GenerateEvent(organization.Id, missingProjectId, stack.Id, occurrenceDate: utcNow);
        projectOrphanBeforeCutoff.CreatedUtc = beforeCutoffUtc;

        string missingOrganizationId = ObjectId.GenerateNewId().ToString();
        var organizationOrphanAtCutoff = _eventData.GenerateEvent(missingOrganizationId, project.Id, stack.Id, occurrenceDate: utcNow);
        organizationOrphanAtCutoff.CreatedUtc = cutoffUtc;
        var secondOrganizationOrphanAtCutoff = _eventData.GenerateEvent(ObjectId.GenerateNewId().ToString(), project.Id, stack.Id, occurrenceDate: utcNow);
        secondOrganizationOrphanAtCutoff.CreatedUtc = cutoffUtc;
        var organizationOrphanBeforeCutoff = _eventData.GenerateEvent(missingOrganizationId, project.Id, stack.Id, occurrenceDate: utcNow);
        organizationOrphanBeforeCutoff.CreatedUtc = beforeCutoffUtc;

        await _eventRepository.AddAsync([
            validEvent,
            stackOrphanAtCutoff,
            stackOrphanBeforeCutoff,
            stackOrphanWithOldOccurrence,
            projectOrphanAtCutoff,
            secondProjectOrphanAtCutoff,
            projectOrphanBeforeCutoff,
            organizationOrphanAtCutoff,
            secondOrganizationOrphanAtCutoff,
            organizationOrphanBeforeCutoff
        ], o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var remainingEvents = await _eventRepository.GetAllAsync(o => o.PageLimit(10).ImmediateConsistency());
        Assert.Equal(4, remainingEvents.Total);
        Assert.Contains(remainingEvents.Documents, e => e.Id == validEvent.Id);
        Assert.Contains(remainingEvents.Documents, e => e.Id == stackOrphanBeforeCutoff.Id);
        Assert.Contains(remainingEvents.Documents, e => e.Id == projectOrphanBeforeCutoff.Id);
        Assert.Contains(remainingEvents.Documents, e => e.Id == organizationOrphanBeforeCutoff.Id);
        Assert.DoesNotContain(remainingEvents.Documents, e => e.Id == stackOrphanAtCutoff.Id);
        Assert.DoesNotContain(remainingEvents.Documents, e => e.Id == stackOrphanWithOldOccurrence.Id);
        Assert.DoesNotContain(remainingEvents.Documents, e => e.Id == projectOrphanAtCutoff.Id);
        Assert.DoesNotContain(remainingEvents.Documents, e => e.Id == organizationOrphanAtCutoff.Id);
    }

    [Fact]
    public async Task PublicCleanupMethods_ScopeCardinalityAndTermsDiscoveryToRecentIngest()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);

        var organization = await _organizationRepository.AddAsync(
            _organizationData.GenerateSampleOrganization(_billingManager, _plans),
            o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(
            _projectData.GenerateProject(organizationId: organization.Id),
            o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(
            _stackData.GenerateStack(projectId: project.Id, organizationId: organization.Id),
            o => o.ImmediateConsistency());

        var validEvent = _eventData.GenerateEvent(organization.Id, project.Id, stack.Id, occurrenceDate: utcNow);
        validEvent.CreatedUtc = utcNow.UtcDateTime;

        var historicalCreatedUtc = utcNow.UtcDateTime.Subtract(RecentIngestWindow).AddMilliseconds(-1);
        var historicalOccurrenceUtc = utcNow.Subtract(TimeSpan.FromDays(30));
        var events = new List<PersistentEvent>(1000) { validEvent };
        for (int index = 0; index < 999; index++)
        {
            var historicalOrphan = _eventData.GenerateEvent(
                ObjectId.GenerateNewId().ToString(),
                ObjectId.GenerateNewId().ToString(),
                ObjectId.GenerateNewId().ToString(),
                occurrenceDate: historicalOccurrenceUtc);
            historicalOrphan.CreatedUtc = historicalCreatedUtc;
            events.Add(historicalOrphan);
        }

        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        Log.Reset();
        var job = CreateJob();
        var context = new JobContext(TestCancellationToken);

        // Act
        await job.DeleteOrphanedEventsByStackAsync(context);
        await job.DeleteOrphanedEventsByProjectAsync(context);
        await job.DeleteOrphanedEventsByOrganizationAsync(context);

        // Assert
        var cleanupLogs = Log.LogEntries
            .Where(entry => entry.CategoryName == typeof(CleanupOrphanedDataJob).FullName)
            .ToList();
        Assert.Contains(cleanupLogs, entry =>
            entry.Message.StartsWith("0/1: Did not find any missing stacks out of 1", StringComparison.Ordinal));
        Assert.Contains(cleanupLogs, entry =>
            entry.Message.StartsWith("Deleted 0 recent events referencing missing stacks out of 1 distinct stack IDs since", StringComparison.Ordinal));
        Assert.Contains(cleanupLogs, entry =>
            entry.Message.StartsWith("0/1: Did not find any missing projects out of 1", StringComparison.Ordinal));
        Assert.Contains(cleanupLogs, entry =>
            entry.Message.StartsWith("Deleted 0 recent events referencing missing projects out of 1 distinct project IDs since", StringComparison.Ordinal));
        Assert.Contains(cleanupLogs, entry =>
            entry.Message.StartsWith("0/1: Did not find any missing organizations out of 1", StringComparison.Ordinal));
        Assert.Contains(cleanupLogs, entry =>
            entry.Message.StartsWith("Deleted 0 recent events referencing missing organizations out of 1 distinct organization IDs since", StringComparison.Ordinal));
        Assert.Equal(1000, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task FixDuplicateStacks_WithDuplicatesAcrossTenants_MergesCorrectly()
    {
        // Arrange - Two stacks in the same project with the same signature (duplicate)
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization.Id);
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        string signatureHash = "abc123def456";
        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id, signatureHash: signatureHash);
        stack1.CreatedUtc = DateTime.UtcNow.AddDays(-10);
        stack1.TotalOccurrences = 5;
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id, signatureHash: signatureHash);
        stack2.CreatedUtc = DateTime.UtcNow.AddDays(-5);
        stack2.TotalOccurrences = 10;
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        // Events on both stacks
        var events1 = _eventData.GenerateEvents(3, organization.Id, project.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(7, organization.Id, project.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - One stack should be deleted, all events should point to the surviving stack
        await RefreshDataAsync();
        var allStacks = await _stackRepository.GetAllAsync(o => o.IncludeSoftDeletes());
        var activeStacks = allStacks.Documents.Where(s => !s.IsDeleted).ToList();
        var deletedStacks = allStacks.Documents.Where(s => s.IsDeleted).ToList();
        Assert.Single(activeStacks);
        Assert.Single(deletedStacks);

        // All events should now reference the surviving stack
        var allEvents = await _eventRepository.GetAllAsync();
        Assert.Equal(10, allEvents.Total);
        Assert.All(allEvents.Documents, e => Assert.Equal(activeStacks[0].Id, e.StackId));
    }

    [Fact]
    public async Task FixDuplicateStacks_NoDuplicates_DoesNotModifyAnything()
    {
        // Arrange - Two stacks with different signatures across two tenants
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: organization1.Id, projectId: project1.Id, signatureHash: "unique_hash_1");
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id, signatureHash: "unique_hash_2");
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(20, organization1.Id, project1.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(20, organization2.Id, project2.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Nothing deleted
        var allStacks = await _stackRepository.GetAllAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(2, allStacks.Total);
        Assert.All(allStacks.Documents, s => Assert.False(s.IsDeleted));

        var totalEvents = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(40, totalEvents);
    }

    [Fact]
    public async Task RunAsync_AllOrphanTypes_CleansUpCorrectly()
    {
        // Arrange - Complex scenario: valid data, orphaned by stack, orphaned by project, orphaned by organization
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var validStack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        await _stackRepository.AddAsync(validStack, o => o.ImmediateConsistency());

        // 100 valid events
        var validEvents = _eventData.GenerateEvents(100, organization1.Id, project1.Id, validStack.Id).ToList();

        // 25 orphaned by stack (stack doesn't exist)
        string fakeStack = ObjectId.GenerateNewId().ToString();
        var orphanedByStack = _eventData.GenerateEvents(25, organization1.Id, project1.Id, fakeStack).ToList();

        // 25 orphaned by project (project doesn't exist)
        string fakeProject = ObjectId.GenerateNewId().ToString();
        string fakeStack2 = ObjectId.GenerateNewId().ToString();
        var orphanedByProject = _eventData.GenerateEvents(25, organization1.Id, fakeProject, fakeStack2).ToList();

        // 25 orphaned by organization (organization doesn't exist)
        string fakeOrganizationId = ObjectId.GenerateNewId().ToString();
        string fakeProject2 = ObjectId.GenerateNewId().ToString();
        string fakeStack3 = ObjectId.GenerateNewId().ToString();
        var orphanedByOrganization = _eventData.GenerateEvents(25, fakeOrganizationId, fakeProject2, fakeStack3).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedByStack).Concat(orphanedByProject).Concat(orphanedByOrganization), o => o.ImmediateConsistency());

        var totalBefore = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(175, totalBefore);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Only 100 valid events remain
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task RunAsync_NoOrphans_PreservesEverything()
    {
        // Arrange - Two complete tenants, no orphans anywhere
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(200, organization1.Id, project1.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(200, organization2.Id, project2.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - All 400 events preserved
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(400, totalAfter);
    }

    [Fact]
    public async Task RunAsync_EmptyDatabase_HealthAllowsScheduleAndGrace()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(0, totalAfter);

        var health = await _job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal("Job has completed successfully in the last 9 hours.", health.Description);

        TimeProvider.Advance(TimeSpan.FromHours(9));
        health = await _job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal("Job has completed successfully in the last 9 hours.", health.Description);

        TimeProvider.Advance(TimeSpan.FromMilliseconds(1));
        health = await _job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Equal("Job has not completed successfully in the last 9 hours.", health.Description);
    }

    [Fact]
    public async Task RunAsync_NoRecentEventsOrDuplicateStacks_UpdatesHealth()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);

        var organization = await _organizationRepository.AddAsync(
            _organizationData.GenerateSampleOrganization(_billingManager, _plans),
            o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(
            _projectData.GenerateProject(organizationId: organization.Id),
            o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(
            _stackData.GenerateStack(projectId: project.Id, organizationId: organization.Id),
            o => o.ImmediateConsistency());
        var historicalEvent = _eventData.GenerateEvent(organization.Id, project.Id, stack.Id, occurrenceDate: utcNow.Subtract(TimeSpan.FromDays(30)));
        historicalEvent.CreatedUtc = utcNow.UtcDateTime.Subtract(RecentIngestWindow).AddMilliseconds(-1);
        await _eventRepository.AddAsync(historicalEvent, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(1, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
        var health = await _job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal("Job has completed successfully in the last 9 hours.", health.Description);
    }

    [Fact]
    public async Task RunAsync_ElasticsearchFailure_DoesNotRefreshHealth()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);

        var job = CreateJob();

        Assert.Equal(JobResult.Success, await job.RunAsync(TestCancellationToken));
        TimeProvider.Advance(TimeSpan.FromHours(9) + TimeSpan.FromMilliseconds(1));

        var failingClient = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://127.0.0.1:1"))
            .MaximumRetries(0)
            .RequestTimeout(TimeSpan.FromMilliseconds(100)));
        typeof(CleanupOrphanedDataJob)
            .GetField("_elasticClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(job, failingClient);

        // Act
        var exception = await Assert.ThrowsAsync<ApplicationException>(() => job.RunAsync(TestCancellationToken));

        // Assert
        Assert.StartsWith("Error getting stack cardinality:", exception.Message, StringComparison.Ordinal);
        var health = await job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Equal("Job has not completed successfully in the last 9 hours.", health.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_LongRunningJob_UsesActiveProgressWithoutMaskingFailure()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);
        var job = CreateJob();
        typeof(CleanupOrphanedDataJob).GetField("_lastSuccessfulRunUtc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, utcNow.UtcDateTime.AddHours(-10));
        typeof(CleanupOrphanedDataJob).GetField("_lastRunProgressUtc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, utcNow.UtcDateTime);
        typeof(CleanupOrphanedDataJob).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, true);

        // Act / Assert
        var health = await job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal("Job is running and has reported progress in the last 3 hours.", health.Description);

        TimeProvider.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMilliseconds(1));
        health = await job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Equal("Job is running but has not reported progress in the last 3 hours.", health.Description);

        typeof(CleanupOrphanedDataJob).GetField("_lastRunProgressUtc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, null);
        health = await job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Equal("Job is running but has not reported progress in the last 3 hours.", health.Description);

        typeof(CleanupOrphanedDataJob).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, false);
        health = await job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Equal("Job has not completed successfully in the last 9 hours.", health.Description);
    }

    [Fact]
    public async Task RunAsync_FirstElasticsearchFailure_HealthReportsNoSuccessfulCompletion()
    {
        // Arrange
        var job = CreateJob();
        var failingClient = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://127.0.0.1:1"))
            .MaximumRetries(0)
            .RequestTimeout(TimeSpan.FromMilliseconds(100)));
        typeof(CleanupOrphanedDataJob)
            .GetField("_elasticClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(job, failingClient);

        // Act
        await Assert.ThrowsAsync<ApplicationException>(() => job.RunAsync(TestCancellationToken));

        // Assert
        var health = await job.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal("Job has not completed successfully yet.", health.Description);
    }

    [Fact]
    public async Task RunAsync_AfterLookbackElapsedWithoutRun_SkipsOlderOrphans()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;
        TimeProvider.SetUtcNow(utcNow);

        var orphanedEvent = _eventData.GenerateEvent(
            ObjectId.GenerateNewId().ToString(),
            ObjectId.GenerateNewId().ToString(),
            ObjectId.GenerateNewId().ToString(),
            occurrenceDate: utcNow);
        orphanedEvent.CreatedUtc = utcNow.UtcDateTime;
        await _eventRepository.AddAsync(orphanedEvent, o => o.ImmediateConsistency());

        TimeProvider.Advance(RecentIngestWindow + TimeSpan.FromMilliseconds(1));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(1, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public void EnsureValidDeleteResponse_UnrecoverableFailure_Throws()
    {
        // Arrange
        var response = new DeleteByQueryResponse
        {
            Failures = [
                new BulkIndexByScrollFailure(
                    new ErrorCause("search_phase_execution_exception") { Reason = "shard failed" },
                    "event-id",
                    "events-v1-2026.07.30",
                    500)
            ]
        };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidDeleteResponse(response, "deleting orphaned events"));

        // Assert
        Assert.Equal("Error deleting orphaned events: Elasticsearch reported 1 unrecoverable delete failures.", exception.Message);
    }

    [Fact]
    public void EnsureValidDeleteResponse_Timeout_Throws()
    {
        // Arrange
        var response = new DeleteByQueryResponse { TimedOut = true };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidDeleteResponse(response, "deleting orphaned events"));

        // Assert
        Assert.Equal("Error deleting orphaned events: Elasticsearch timed out before completing the delete.", exception.Message);
    }

    [Fact]
    public void EnsureValidSearchResponse_Timeout_Throws()
    {
        // Arrange
        var response = new SearchResponse { TimedOut = true };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidSearchResponse(response, "getting orphaned events"));

        // Assert
        Assert.Equal("Error getting orphaned events: Elasticsearch timed out before completing the search.", exception.Message);
    }

    [Fact]
    public void EnsureValidSearchResponse_FailedShard_Throws()
    {
        // Arrange
        var response = new SearchResponse
        {
            Shards = new ShardStatistics
            {
                Failed = 1,
                Successful = 1,
                Total = 2
            }
        };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidSearchResponse(response, "getting orphaned events"));

        // Assert
        Assert.Equal("Error getting orphaned events: Elasticsearch reported 1 failed search shards.", exception.Message);
    }
}
