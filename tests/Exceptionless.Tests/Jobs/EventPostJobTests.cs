using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
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

    [Fact]
    public async Task CanRunJob_WhenProjectOverLimit_BlocksEvents()
    {
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        project.IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 1 };
        project.GetCurrentUsage(TimeProvider).Total = 1;
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency().Cache());

        Assert.NotNull(await EnqueueEventPostAsync(GenerateEvent()));
        Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);

        var result = await _job.RunAsync(TestCancellationToken);
        Assert.True(result.IsSuccess);

        await RefreshDataAsync();
        Assert.Equal(0, await _eventRepository.CountAsync());

        var usage = await _usageService.GetUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId);
        Assert.Equal(1, usage.CurrentUsage.Blocked);
    }

    [Fact]
    public async Task CanRunJob_WhenSmartThrottleApplied_BlocksEventsOnce()
    {
        var organization = await _organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(organization);
        organization.MaxEventsPerMonth = 1_000_000;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id), o => o.ImmediateConsistency().Cache());
        await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id), o => o.ImmediateConsistency().Cache());
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var endOfMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        double windowsLeft = Math.Max(1, Math.Ceiling((endOfMonth - utcNow).TotalMinutes / 5));
        int currentWindowSpike = (int)Math.Floor(organization.MaxEventsPerMonth / windowsLeft * 10 * 0.9);
        await _usageService.IncrementTotalAsync(organization, project.Id, currentWindowSpike);

        var throttleResult = await _usageService.GetSmartThrottleRateAsync(TestConstants.OrganizationId, TestConstants.ProjectId);
        Assert.True(throttleResult.IsThrottled);
        Assert.Equal(0.05, throttleResult.SampleRate);

        var events = Enumerable.Range(0, 100).Select(index => GenerateEvent(source: $"source-{index}")).ToList();
        Assert.NotNull(await EnqueueEventPostAsync(events));

        var result = await _job.RunAsync(TestCancellationToken);
        Assert.True(result.IsSuccess);

        var usage = await _usageService.GetUsageAsync(TestConstants.OrganizationId, TestConstants.ProjectId);
        Assert.True(usage.CurrentUsage.Blocked > 0);
        Assert.Equal(0, usage.CurrentUsage.Discarded);

        await RefreshDataAsync();
        Assert.InRange(await _eventRepository.CountAsync(), 1, 20);
    }

    [Fact]
    public void SmartThrottleSelection_SingleEventPosts_UsesStableFivePercentSample()
    {
        var persistentEvent = GenerateEvent();

        int accepted = Enumerable.Range(0, 1_000)
            .Count(index => EventPostsJob.IsSelectedForSmartThrottle($"post-{index}", persistentEvent, 0));

        Assert.InRange(accepted, 30, 70);
    }
}
