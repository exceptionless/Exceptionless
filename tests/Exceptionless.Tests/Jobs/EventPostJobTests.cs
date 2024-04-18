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
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.Jobs;

public class EventPostJobTests : IntegrationTestsBase
{
    private readonly EventPostsJob _job;
    private readonly IFileStorage _storage;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IQueue<EventPost> _eventQueue;
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
        _eventPostService = new EventPostService(_eventQueue, _storage, Log);
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _eventRepository = GetService<IEventRepository>();
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
        var files = await _storage.GetFileListAsync();
        Assert.Single(files);

        var result = await _job.RunAsync();
        Assert.True(result.IsSuccess);

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(1, stats.Completed);

        await RefreshDataAsync();
        Assert.Equal(1, await _eventRepository.CountAsync());

        files = await _storage.GetFileListAsync();
        Assert.Empty(files);
    }

    [Fact]
    public async Task CanRunJobWithDiscardedEventUsage()
    {
        var organization = await _organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        var usage = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(0, usage.CurrentUsage.Total);

        usage = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(0, usage.CurrentUsage.Total);
        Assert.Equal(0, usage.CurrentUsage.Blocked);

        var ev = GenerateEvent(type: Event.KnownTypes.Log, source: "test", userIdentity: "test1");
        Assert.NotNull(await EnqueueEventPostAsync(ev));

        var result = await _job.RunAsync();
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
        logStack.Status = StackStatus.Discarded;
        await _stackRepository.SaveAsync(logStack, o => o.ImmediateConsistency());

        var sessionStack = await _stackRepository.GetByIdAsync(sessionEvent.StackId);
        sessionStack.Status = StackStatus.Discarded;
        await _stackRepository.SaveAsync(sessionStack, o => o.ImmediateConsistency());

        // Verify job processed discarded events.
        Assert.NotNull(await EnqueueEventPostAsync([
            GenerateEvent(type: Event.KnownTypes.Session, sessionId: "abcdefghi"),
            GenerateEvent(type: Event.KnownTypes.Log, source: "test", sessionId: "abcdefghi"),
            GenerateEvent(type: Event.KnownTypes.Log, source: "test", userIdentity: "test3")
        ]));

        result = await _job.RunAsync();
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
        var files = await _storage.GetFileListAsync();
        Assert.Single(files);

        var result = await _job.RunAsync();
        Assert.False(result.IsSuccess);

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(1, stats.Completed);

        files = await _storage.GetFileListAsync();
        Assert.Empty(files);
    }

    [Fact]
    public async Task CanRunJobWithNonExistingEventDataAsync()
    {
        var ev = GenerateEvent();
        Assert.NotNull(await EnqueueEventPostAsync(ev));
        Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);

        await _storage.DeleteFilesAsync(await _storage.GetFileListAsync());

        var result = await _job.RunAsync();
        Assert.False(result.IsSuccess);

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(1, stats.Abandoned);
    }

    private async Task CreateDataAsync(BillingPlan? plan = null)
    {
        foreach (var organization in OrganizationData.GenerateSampleOrganizations(_billingManager, _plans))
        {
            if (plan is not null)
                _billingManager.ApplyBillingPlan(organization, plan, UserData.GenerateSampleUser());
            else if (organization.Id == TestConstants.OrganizationId3)
                _billingManager.ApplyBillingPlan(organization, _plans.FreePlan, UserData.GenerateSampleUser());
            else
                _billingManager.ApplyBillingPlan(organization, _plans.SmallPlan, UserData.GenerateSampleUser());

            if (organization.BillingPrice > 0)
            {
                organization.StripeCustomerId = "stripe_customer_id";
                organization.CardLast4 = "1234";
                organization.SubscribeDate = SystemClock.UtcNow;
                organization.BillingChangeDate = SystemClock.UtcNow;
                organization.BillingChangedByUserId = TestConstants.UserId;
            }

            if (organization.IsSuspended)
            {
                organization.SuspendedByUserId = TestConstants.UserId;
                organization.SuspensionCode = SuspensionCode.Billing;
                organization.SuspensionDate = SystemClock.UtcNow;
            }

            await _organizationRepository.AddAsync(organization, o => o.Cache().ImmediateConsistency());
        }

        await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.Cache().ImmediateConsistency());

        foreach (var user in UserData.GenerateSampleUsers())
        {
            if (user.Id == TestConstants.UserId)
            {
                user.OrganizationIds.Add(TestConstants.OrganizationId2);
                user.OrganizationIds.Add(TestConstants.OrganizationId3);
            }

            if (!user.IsEmailAddressVerified)
                user.MarkEmailAddressUnverified();

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

    private static PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string? userIdentity = null, string? type = null, string? source = null, string? sessionId = null)
    {
        occurrenceDate ??= SystemClock.OffsetNow;
        return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, source: source, sessionId: sessionId);
    }
}
