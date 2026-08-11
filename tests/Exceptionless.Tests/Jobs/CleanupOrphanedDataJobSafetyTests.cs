using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class CleanupOrphanedDataJobSafetyTests : IntegrationTestsBase
{
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
    private readonly ExceptionlessElasticConfiguration _configuration;

    public CleanupOrphanedDataJobSafetyTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
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
        _configuration = GetService<ExceptionlessElasticConfiguration>();
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStackAsync_WithUnavailableStackIndex_FailsWithoutDeletingEvents()
    {
        string stackId = await CreateValidEventAsync();

        await AssertUnavailableParentIndexPreservesEventsAsync(
            _configuration.Stacks.VersionedName,
            context => _job.DeleteOrphanedEventsByStackAsync(context),
            stackId);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProjectAsync_WithUnavailableProjectIndex_FailsWithoutDeletingEvents()
    {
        string stackId = await CreateValidEventAsync();

        await AssertUnavailableParentIndexPreservesEventsAsync(
            _configuration.Projects.VersionedName,
            context => _job.DeleteOrphanedEventsByProjectAsync(context),
            stackId);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganizationAsync_WithUnavailableOrganizationIndex_FailsWithoutDeletingEvents()
    {
        string stackId = await CreateValidEventAsync();

        await AssertUnavailableParentIndexPreservesEventsAsync(
            _configuration.Organizations.VersionedName,
            context => _job.DeleteOrphanedEventsByOrganizationAsync(context),
            stackId);
    }

    private async Task<string> CreateValidEventAsync()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(
            _eventData.GenerateEvent(organization.Id, project.Id, stack.Id),
            o => o.ImmediateConsistency());

        return stack.Id;
    }

    private async Task AssertUnavailableParentIndexPreservesEventsAsync(
        string parentIndex,
        Func<JobContext, Task> cleanup,
        string stackId)
    {
        var closeResponse = await _configuration.Client.Indices.CloseAsync(parentIndex, TestContext.Current.CancellationToken);
        Assert.True(closeResponse.IsValidResponse, closeResponse.DebugInformation);

        try
        {
            var exception = await Assert.ThrowsAsync<DocumentException>(() => cleanup(new JobContext(TestCancellationToken)));
            Assert.StartsWith("Error getting document ", exception.Message);
        }
        finally
        {
            var openResponse = await _configuration.Client.Indices.OpenAsync(parentIndex, TestContext.Current.CancellationToken);
            Assert.True(openResponse.IsValidResponse, openResponse.DebugInformation);
        }

        await RefreshDataAsync();
        Assert.Equal(1, await _eventRepository.CountAsync(q => q.Stack(stackId), o => o.ImmediateConsistency()));
    }
}
