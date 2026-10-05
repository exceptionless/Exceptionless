using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Foundatio.Storage;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.Jobs;

public class EventPostJobTests : IntegrationTestsBase
{
    private readonly EventPostsJob _job;
    private readonly IFileStorage _storage;
    private readonly OrganizationData _organizationData;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ProjectData _projectData;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;
    private readonly IQueue<EventPost> _eventQueue;
    private readonly UserData _userData;
    private readonly IUserRepository _userRepository;
    private readonly UsageService _usageService;
    private readonly ITextSerializer _serializer;
    private readonly EventPostService _eventPostService;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly AppOptions _options;

    public EventPostJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<EventPostsJob>();
        _eventQueue = GetService<IQueue<EventPost>>();
        _storage = GetService<IFileStorage>();
        _eventPostService = new EventPostService(_eventQueue, _storage, TimeProvider, Log);
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _userData = GetService<UserData>();
        _userRepository = GetService<IUserRepository>();
        _usageService = GetService<UsageService>();
        _serializer = GetService<ITextSerializer>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
        _options = GetService<AppOptions>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await _eventQueue.DeleteQueueAsync();
        await CreateDataAsync();
    }

    [Fact]
    public async Task CanRunJob()
    {
        var ev = GenerateEvent();
        Assert.NotNull(await EnqueueEventPostAsync(ev));
        Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
        var files = await _storage.GetFileListAsync(cancellationToken: TestCancellationToken);
        Assert.Single(files);

        var result = await _job.RunAsync(TestCancellationToken);
        Assert.True(result.IsSuccess);

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(1, stats.Completed);

        await RefreshDataAsync();
        Assert.Equal(1, await _eventRepository.CountAsync());

        files = await _storage.GetFileListAsync(cancellationToken: TestCancellationToken);
        Assert.Empty(files);
    }

    [Fact]
    public async Task CanRunJobWithDiscardedEventUsage()
    {
        var organization = await _organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(organization);
        var usage = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(0, usage.CurrentUsage.Total);

        usage = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(0, usage.CurrentUsage.Total);
        Assert.Equal(0, usage.CurrentUsage.Blocked);

        var ev = GenerateEvent(type: Event.KnownTypes.Log, source: "test", userIdentity: "test1");
        Assert.NotNull(await EnqueueEventPostAsync(ev));

        var result = await _job.RunAsync(TestCancellationToken);
        Assert.True(result.IsSuccess);

        await RefreshDataAsync();
        var events = await _eventRepository.GetAllAsync();
        Assert.Equal(2, events.Total);
        var logEvent = events.Documents.Single(e => String.Equals(e.Type, Event.KnownTypes.Log));
        Assert.NotNull(logEvent);
        var sessionEvent = events.Documents.Single(e => String.Equals(e.Type, Event.KnownTypes.Session));
        Assert.NotNull(sessionEvent);

        usage = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(1, usage.CurrentUsage.Total);
        Assert.Equal(0, usage.CurrentUsage.Blocked);

        // Mark the stack as discarded
        var logStack = await _stackRepository.GetByIdAsync(logEvent.StackId);
        Assert.NotNull(logStack);
        logStack.Status = StackStatus.Discarded;
        await _stackRepository.SaveAsync(logStack, o => o.ImmediateConsistency());

        var sessionStack = await _stackRepository.GetByIdAsync(sessionEvent.StackId);
        Assert.NotNull(sessionStack);
        sessionStack.Status = StackStatus.Discarded;
        await _stackRepository.SaveAsync(sessionStack, o => o.ImmediateConsistency());

        // Verify job processed discarded events.
        Assert.NotNull(await EnqueueEventPostAsync([
            GenerateEvent(type: Event.KnownTypes.Session, sessionId: "abcdefghi"),
            GenerateEvent(type: Event.KnownTypes.Log, source: "test", sessionId: "abcdefghi"),
            GenerateEvent(type: Event.KnownTypes.Log, source: "test", userIdentity: "test3")
        ]));

        result = await _job.RunAsync(TestCancellationToken);
        Assert.True(result.IsSuccess);

        await RefreshDataAsync();
        events = await _eventRepository.GetAllAsync();
        Assert.Equal(3, events.Total);

        usage = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(1, usage.CurrentUsage.Total);
        Assert.Equal(0, usage.CurrentUsage.Blocked);
    }

    [Fact]
    public async Task RunAsync_RetryingParsedBatch_PreservesEnvironmentDataWithoutDuplicates()
    {
        var production = GenerateEvent(type: Event.KnownTypes.Log);
        production.Environment = "Production";
        var staging = GenerateEvent(type: Event.KnownTypes.Log);
        staging.Environment = "Staging";
        await EnqueueEventPostAsync([production, staging]);

        var services = GetService<IServiceProvider>();
        var failingPipeline = new FailingPipeline(services, _options, Log);
        var failingJob = ActivatorUtilities.CreateInstance<EventPostsJob>(services, failingPipeline);
        Assert.True((await failingJob.RunAsync(TestCancellationToken)).IsSuccess);
        Assert.Equal(3, (await _eventQueue.GetQueueStatsAsync()).Enqueued);

        Assert.True((await _job.RunAsync(TestCancellationToken)).IsSuccess);
        Assert.True((await _job.RunAsync(TestCancellationToken)).IsSuccess);
        await RefreshDataAsync();

        var events = (await _eventRepository.GetAllAsync()).Documents;
        Assert.Equal(2, events.Count);
        Assert.All(events, ev =>
        {
            Assert.NotNull(ev.Data);
            Assert.Equal(ev.Environment, ev.Data["environment"]);
            Assert.False(ev.Data.ContainsKey("environment1"));
        });
    }

    private sealed class FailingPipeline(IServiceProvider services, AppOptions options, ILoggerFactory loggerFactory)
        : EventPipeline(services, options, loggerFactory)
    {
        protected override IList<Type> GetActionTypes() => [];

        public override Task<ICollection<EventContext>> RunAsync(ICollection<EventContext> contexts)
        {
            foreach (var context in contexts)
                context.SetError("Simulated transient batch failure", new InvalidOperationException("Retry this event"));

            return Task.FromResult(contexts);
        }
    }

    [Fact]
    public async Task CanRunJobWithMassiveEventAsync()
    {
        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();

        for (int i = 1; i < 100; i++)
            ev.Data[$"{i}MB"] = new string('0', 1024 * 1000);

        Assert.NotNull(await EnqueueEventPostAsync(ev));
        Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
        var files = await _storage.GetFileListAsync(cancellationToken: TestCancellationToken);
        Assert.Single(files);

        var result = await _job.RunAsync(TestCancellationToken);
        Assert.False(result.IsSuccess);

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(1, stats.Completed);

        files = await _storage.GetFileListAsync(cancellationToken: TestCancellationToken);
        Assert.Empty(files);
    }

    [Fact]
    public async Task CanRunJobWithNonExistingEventDataAsync()
    {
        var ev = GenerateEvent();
        Assert.NotNull(await EnqueueEventPostAsync(ev));
        Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);

        await _storage.DeleteFilesAsync(await _storage.GetFileListAsync(cancellationToken: TestCancellationToken));

        var result = await _job.RunAsync(TestCancellationToken);
        Assert.False(result.IsSuccess);

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(1, stats.Abandoned);
    }

    private async Task CreateDataAsync(BillingPlan? plan = null)
    {
        foreach (var organization in _organizationData.GenerateSampleOrganizations(_billingManager, _plans))
        {
            if (plan is not null)
                _billingManager.ApplyBillingPlan(organization, plan, _userData.GenerateSampleUser());
            else if (organization.Id == TestConstants.OrganizationId3)
                _billingManager.ApplyBillingPlan(organization, _plans.FreePlan, _userData.GenerateSampleUser());
            else
                _billingManager.ApplyBillingPlan(organization, _plans.SmallPlan, _userData.GenerateSampleUser());

            if (organization.BillingPrice > 0)
            {
                organization.StripeCustomerId = "stripe_customer_id";
                organization.CardLast4 = "1234";
                organization.SubscribeDate = DateTime.UtcNow;
                organization.BillingChangeDate = DateTime.UtcNow;
                organization.BillingChangedByUserId = TestConstants.UserId;
            }

            if (organization.IsSuspended)
            {
                organization.SuspendedByUserId = TestConstants.UserId;
                organization.SuspensionCode = SuspensionCode.Billing;
                organization.SuspensionDate = DateTime.UtcNow;
            }

            await _organizationRepository.AddAsync(organization, o => o.Cache().ImmediateConsistency());
        }

        await _projectRepository.AddAsync(_projectData.GenerateSampleProjects(), o => o.Cache().ImmediateConsistency());

        foreach (var user in _userData.GenerateSampleUsers())
        {
            if (user.Id == TestConstants.UserId)
            {
                user.OrganizationIds.Add(TestConstants.OrganizationId2);
                user.OrganizationIds.Add(TestConstants.OrganizationId3);
            }

            if (!user.IsEmailAddressVerified)
                user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

            await _userRepository.AddAsync(user, o => o.Cache().ImmediateConsistency());
        }
    }

    private Task<string?> EnqueueEventPostAsync(PersistentEvent ev)
    {
        return EnqueueEventPostAsync([ev]);
    }

    private Task<string?> EnqueueEventPostAsync(List<PersistentEvent> ev)
    {
        var first = ev.First();

        var eventPostInfo = new EventPost(_options.EnableArchive)
        {
            OrganizationId = first.OrganizationId,
            ProjectId = first.ProjectId,
            ApiVersion = 2,
            CharSet = "utf-8",
            ContentEncoding = "gzip",
            MediaType = "application/json",
            UserAgent = "exceptionless-test",
        };

        var stream = new MemoryStream(_serializer.SerializeToBytes(ev).Compress());
        return _eventPostService.EnqueueAsync(eventPostInfo, stream);
    }

    private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string? userIdentity = null, string? type = null, string? source = null, string? sessionId = null)
    {
        occurrenceDate ??= DateTimeOffset.Now;
        return _eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, source: source, sessionId: sessionId);
    }
}
