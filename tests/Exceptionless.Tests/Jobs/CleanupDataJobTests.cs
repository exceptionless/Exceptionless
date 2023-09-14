﻿using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Jobs;

public class CleanupDataJobTests : IntegrationTestsBase
{
    private readonly CleanupDataJob _job;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public CleanupDataJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<CleanupDataJob>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _eventRepository = GetService<IEventRepository>();
        _tokenRepository = GetService<ITokenRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens()
    {
        var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);
        organization.IsSuspended = true;
        organization.SuspensionDate = SystemClock.UtcNow;
        organization.SuspendedByUserId = "1";
        organization.SuspensionCode = Core.Models.SuspensionCode.Billing;
        organization.SuspensionNotes = "blah";
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(ProjectData.GenerateSampleProject());
        var token = await _tokenRepository.AddAsync(TokenData.GenerateSampleApiKeyToken(), o => o.ImmediateConsistency());
        Assert.False(token.IsSuspended);

        await _job.RunAsync();

        token = await _tokenRepository.GetByIdAsync(token.Id);
        Assert.True(token.IsSuspended);
    }

    [Fact]
    public async Task CanCleanupSoftDeletedOrganization()
    {
        var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);
        organization.IsDeleted = true;
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(ProjectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(StackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(EventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync();

        Assert.Null(await _organizationRepository.GetByIdAsync(organization.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedProject()
    {
        var organization = await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = ProjectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(StackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(EventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync();

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack()
    {
        var organization = await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(ProjectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = StackData.GenerateSampleStack();
        stack.IsDeleted = true;
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var persistentEvent = await _eventRepository.AddAsync(EventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync();

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupEventsOutsideOfRetentionPeriod()
    {
        var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(ProjectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(StackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();
        var date = SystemClock.OffsetUtcNow.SubtractDays(options.MaximumRetentionDays);
        var persistentEvent = await _eventRepository.AddAsync(EventData.GenerateEvent(organization.Id, project.Id, stack.Id, date, date, date), o => o.ImmediateConsistency());

        await _job.RunAsync();

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack.Id));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanDeleteOrphanedEventsByStack()
    {
        var organization = OrganizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(ProjectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(StackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(EventData.GenerateEvents(5000, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var orphanedEvents = EventData.GenerateEvents(10000, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());

        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(15000, eventCount);

        await GetService<CleanupOrphanedDataJob>().RunAsync();

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(5000, eventCount);
    }
}
