using System.Diagnostics;
using System.Globalization;
using System.Text;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Storage;
using McSherry.SemanticVersioning;
using Xunit;
using Xunit.Abstractions;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.Pipeline;

public sealed class EventPipelineTests : IntegrationTestsBase
{
    private readonly EventPipeline _pipeline;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;
    private readonly IStackRepository _stackRepository;
    private readonly OrganizationData _organizationData;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ProjectData _projectData;
    private readonly IProjectRepository _projectRepository;
    private readonly UserData _userData;
    private readonly IUserRepository _userRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public EventPipelineTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _stackRepository = GetService<IStackRepository>();
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _userData = GetService<UserData>();
        _userRepository = GetService<IUserRepository>();
        _pipeline = GetService<EventPipeline>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await CreateProjectDataAsync();
    }

    [Fact]
    public async Task NoFutureEventsAsync()
    {
        var localTime = DateTime.UtcNow;
        var ev = GenerateEvent(localTime.AddMinutes(10));

        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);
        Assert.True(ev.Date < localTime.AddMinutes(10));
        Assert.True(ev.Date - localTime < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public Task CreateAutoSessionAsync()
    {
        return CreateAutoSessionInternalAsync(DateTimeOffset.Now);
    }

    private async Task CreateAutoSessionInternalAsync(DateTimeOffset date)
    {
        var ev = GenerateEvent(date, "blake@exceptionless.io");
        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        Assert.False(context.IsCancelled);
        Assert.True(context.IsProcessed);

        await RefreshDataAsync();
        var events = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(2, events.Total);
        Assert.Single(events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());

        var sessionStart = events.Documents.First(e => e.IsSessionStart());
        Assert.Equal(0, sessionStart.Value);
        Assert.False(sessionStart.HasSessionEndTime());
    }

    [Fact]
    public async Task CanUpdateExistingAutoSessionAsync()
    {
        var startDate = DateTimeOffset.Now.SubtractMinutes(5);
        await CreateAutoSessionInternalAsync(startDate);

        var ev = GenerateEvent(startDate.AddMinutes(4), "blake@exceptionless.io");

        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        Assert.False(context.IsCancelled);
        Assert.True(context.IsProcessed);

        await RefreshDataAsync();
        var events = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(3, events.Total);
        Assert.Single(events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());

        var sessionStart = events.Documents.Single(e => e.IsSessionStart());
        Assert.Equal(240, sessionStart.Value);
        Assert.False(sessionStart.HasSessionEndTime());
    }

    [Fact]
    public async Task IgnoreAutoSessionsWithoutIdentityAsync()
    {
        var ev = GenerateEvent(DateTimeOffset.Now);
        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        Assert.False(context.IsCancelled);
        Assert.True(context.IsProcessed);

        await RefreshDataAsync();
        var events = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(1, events.Total);
        Assert.Equal(0, events.Documents.Count(e => e.IsSessionStart()));
        Assert.Empty(events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());
    }

    [Fact]
    public async Task CreateAutoSessionStartEventsAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
                GenerateEvent(firstEventDate.AddSeconds(20), "blake@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(30), "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.DoesNotContain(contexts, c => c.IsCancelled);
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(6, results.Total);
        Assert.Equal(2, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
        Assert.Equal(2, results.Documents.Count(e => e.IsSessionEnd()));

        var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
        Assert.Equal(2, sessionStarts.Count);
        foreach (var sessionStart in sessionStarts)
        {
            Assert.Equal(10, sessionStart.Value);
            Assert.True(sessionStart.HasSessionEndTime());
        }
    }

    [Fact]
    public async Task UpdateAutoMultipleSessionStartEventDurationsAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io", Event.KnownTypes.Session),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.Session),
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c.IsCancelled));
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(1, results.Total);
        Assert.Single(results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());
        Assert.Equal(0, results.Documents.Count(e => e.IsSessionEnd()));

        var sessionStart = results.Documents.Single(e => e.IsSessionStart());
        Assert.Equal(10, sessionStart.Value);
        Assert.False(sessionStart.HasSessionEndTime());
    }

    [Fact]
    public async Task UpdateAutoSessionLastActivityAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var lastEventDate = firstEventDate.Add(TimeSpan.FromMinutes(1));

        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io"),
                GenerateEvent(lastEventDate, "blake@exceptionless.io"),
                GenerateEvent(lastEventDate, "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
                GenerateEvent(lastEventDate),
                GenerateEvent(firstEventDate, "eric@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(30), "eric@exceptionless.io", Event.KnownTypes.Session),
                GenerateEvent(lastEventDate, "eric@exceptionless.io")
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.DoesNotContain(contexts, c => c.IsCancelled);
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.GetAllAsync(o => o.PageLimit(15));
        Assert.Equal(9, results.Total);
        Assert.Equal(2, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd() && e.GetUserIdentity()?.Identity == "blake@exceptionless.io"));
        Assert.Single(results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId()) && e.GetUserIdentity()?.Identity == "eric@exceptionless.io").Select(e => e.GetSessionId()).Distinct());
        Assert.Equal(1, results.Documents.Count(e => String.IsNullOrEmpty(e.GetSessionId())));
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

        var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
        Assert.Equal(2, sessionStarts.Count);

        var firstUserSessionStartEvents = sessionStarts.Single(e => e.GetUserIdentity()?.Identity == "blake@exceptionless.io");
        Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, firstUserSessionStartEvents.Value);
        Assert.True(firstUserSessionStartEvents.HasSessionEndTime());

        var secondUserSessionStartEvents = sessionStarts.Single(e => e.GetUserIdentity()?.Identity == "eric@exceptionless.io");
        Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, secondUserSessionStartEvents.Value);
        Assert.False(secondUserSessionStartEvents.HasSessionEndTime());
    }

    [Fact]
    public async Task CloseExistingAutoSessionAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        string identity = "blake@exceptionless.io";
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, identity),
                GenerateEvent(firstEventDate.AddSeconds(10), identity, Event.KnownTypes.SessionHeartbeat)
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c is { IsCancelled: true, IsDiscarded: true }));
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(2, results.Total);
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));

        events =
        [
            GenerateEvent(firstEventDate.AddSeconds(10), identity, Event.KnownTypes.Session),
            GenerateEvent(firstEventDate.AddSeconds(20), identity, Event.KnownTypes.SessionEnd)
        ];

        await RefreshDataAsync();
        contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c.IsCancelled));
        Assert.Equal(1, contexts.Count(c => c.IsProcessed));

        await RefreshDataAsync();
        results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(3, results.Total);
        var sessionIds = results.Documents.Select(e => e.GetSessionId()).Distinct();
        Assert.Single(sessionIds);
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

        var sessionStart = results.Documents.Single(e => e.IsSessionStart());
        Assert.Equal(20, sessionStart.Value);
        Assert.True(sessionStart.HasSessionEndTime());
    }

    [Fact]
    public async Task IgnoreDuplicateAutoEndSessionsAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionEnd)
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(2, contexts.Count(c => c.IsCancelled));
        Assert.False(contexts.All(c => c.IsProcessed));

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(0, results.Total);
    }

    [Fact]
    public async Task WillMarkAutoSessionHeartbeatStackHiddenAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionHeartbeat)
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c is { IsCancelled: true, IsDiscarded: true }));
        Assert.Equal(0, contexts.Count(c => c.IsProcessed));

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(1, results.Total);
        var sessionStart = results.Documents.FirstOrDefault(e => e.IsSessionStart());
        Assert.NotNull(sessionStart);

        var stack = await _stackRepository.GetByIdAsync(sessionStart.StackId);
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Ignored, stack.Status);
    }

    [Fact]
    public Task CreateManualSessionAsync()
    {
        return CreateManualSessionInternalAsync(DateTimeOffset.Now);
    }

    private async Task CreateManualSessionInternalAsync(DateTimeOffset start)
    {
        var ev = GenerateEvent(start, sessionId: "12345678");
        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        Assert.False(context.IsCancelled);
        Assert.True(context.IsProcessed);

        await RefreshDataAsync();
        var events = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(2, events.Total);
        Assert.Single(events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());

        var sessionStartEvent = events.Documents.SingleOrDefault(e => e.IsSessionStart());
        Assert.NotNull(sessionStartEvent);
        Assert.Equal(0, sessionStartEvent.Value);
        Assert.False(sessionStartEvent.HasSessionEndTime());
    }

    [Fact]
    public async Task CanUpdateExistingManualSessionAsync()
    {
        var startDate = DateTimeOffset.Now.SubtractMinutes(5);
        await CreateManualSessionInternalAsync(startDate);

        var ev = GenerateEvent(startDate.AddMinutes(4), sessionId: "12345678");

        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        Assert.False(context.IsCancelled);
        Assert.True(context.IsProcessed);

        await RefreshDataAsync();
        var events = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(3, events.Total);
        Assert.Single(events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());

        var sessionStart = events.Documents.First(e => e.IsSessionStart());
        Assert.Equal(240, sessionStart.Value);
        Assert.False(sessionStart.HasSessionEndTime());
    }

    [Fact]
    public async Task CreateManualSingleSessionStartEventAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.Session, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(20), type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(30), sessionId: "12345678"),
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.DoesNotContain(contexts, c => c.IsCancelled);
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(4, results.Total);
        Assert.Single(results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());

        var sessionStartEvent = results.Documents.SingleOrDefault(e => e.IsSessionStart());
        Assert.NotNull(sessionStartEvent);
        Assert.Equal(30, sessionStartEvent.Value);
        Assert.True(sessionStartEvent.HasSessionEndTime());
    }

    [Fact]
    public async Task CreateManualSessionStartEventAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, sessionId: "12345678"),
                // This event will be deduplicated as part of the manual session plugin.
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(20), sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(30), type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c.IsCancelled));
        Assert.Equal(3, contexts.Count(c => c.IsProcessed));

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(4, results.Total);
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

        var sessionStartEvent = results.Documents.First(e => e.IsSessionStart());
        Assert.NotNull(sessionStartEvent);
        Assert.Equal(30, sessionStartEvent.Value);
        Assert.True(sessionStartEvent.HasSessionEndTime());
    }

    [Fact]
    public async Task UpdateManualSessionLastActivityAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var lastEventDate = firstEventDate.Add(TimeSpan.FromMinutes(1));

        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, type: Event.KnownTypes.Session, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), sessionId: "12345678"),
                GenerateEvent(lastEventDate, type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.DoesNotContain(contexts, c => c.IsCancelled);
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(3, results.Total);
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));
        Assert.Single(results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));
        Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, results.Documents.First(e => e.IsSessionStart()).Value);
    }

    [Fact]
    public async Task CloseExistingManualSessionAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionHeartbeat, sessionId: "12345678")
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c is { IsCancelled: true, IsDiscarded: true }));
        Assert.Contains(contexts, c => c.IsProcessed);

        events =
        [
            GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.Session, sessionId: "12345678"),
            GenerateEvent(firstEventDate.AddSeconds(20), type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
        ];

        await RefreshDataAsync();
        contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c.IsCancelled));
        Assert.Contains(contexts, c => c.IsProcessed);

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(3, results.Total);
        Assert.Single(results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct());
        Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

        var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
        Assert.Single(sessionStarts);
        foreach (var sessionStart in sessionStarts)
        {
            Assert.Equal(20, sessionStart.Value);
            Assert.True(sessionStart.HasSessionEndTime());
        }
    }

    [Fact]
    public async Task IgnoreDuplicateManualEndSessionsAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(2, contexts.Count(c => c.IsCancelled));
        Assert.False(contexts.All(c => c.IsProcessed));

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(0, results.Total);
    }

    [Fact]
    public async Task WillMarkManualSessionHeartbeatStackHiddenAsync()
    {
        var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
        var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionHeartbeat, sessionId: "12345678")
            };

        var contexts = await _pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.DoesNotContain(contexts, c => c.HasError);
        Assert.Equal(1, contexts.Count(c => c is { IsCancelled: true, IsDiscarded: true }));
        Assert.Equal(0, contexts.Count(c => c.IsProcessed));

        await RefreshDataAsync();
        var results = await _eventRepository.FindAsync(q => q.SortExpression(EventIndex.Alias.Date));
        Assert.Equal(1, results.Total);
        var sessionStart = results.Documents.FirstOrDefault(e => e.IsSessionStart());
        Assert.NotNull(sessionStart);

        var stack = await _stackRepository.GetByIdAsync(sessionStart.StackId);
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Ignored, stack.Status);
    }

    [Fact]
    public void CanIndexExtendedData()
    {
        var ev = _eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: DateTime.UtcNow);
        ev.Data ??= new DataDictionary();
        ev.Data.Add("First Name", "Eric"); // invalid field name
        ev.Data.Add("IsVerified", true);
        ev.Data.Add("IsVerified1", true.ToString());
        ev.Data.Add("Age", Int32.MaxValue);
        ev.Data.Add("Age1", Int32.MaxValue.ToString(CultureInfo.InvariantCulture));
        ev.Data.Add("AgeDec", Decimal.MaxValue);
        ev.Data.Add("AgeDec1", Decimal.MaxValue.ToString(CultureInfo.InvariantCulture));
        ev.Data.Add("AgeDbl", Double.MaxValue);
        ev.Data.Add("AgeDbl1", Double.MaxValue.ToString("r", CultureInfo.InvariantCulture));
        ev.Data.Add(" Birthday ", DateTime.MinValue);
        ev.Data.Add("BirthdayWithOffset", DateTimeOffset.MinValue);
        ev.Data.Add("@excluded", DateTime.MinValue);
        ev.Data.Add("Address", new { State = "Texas" });
        ev.SetSessionId("123456789");

        ev.CopyDataToIndex([]);

        Assert.False(ev.Idx.ContainsKey("first-name-s"));
        Assert.True(ev.Idx.ContainsKey("isverified-b"));
        Assert.True(ev.Idx.ContainsKey("isverified1-b"));
        Assert.True(ev.Idx.ContainsKey("age-n"));
        Assert.True(ev.Idx.ContainsKey("age1-n"));
        Assert.True(ev.Idx.ContainsKey("agedec-n"));
        Assert.True(ev.Idx.ContainsKey("agedec1-n"));
        Assert.True(ev.Idx.ContainsKey("agedbl-n"));
        Assert.True(ev.Idx.ContainsKey("agedbl1-n"));
        Assert.True(ev.Idx.ContainsKey("birthday-d"));
        Assert.True(ev.Idx.ContainsKey("birthdaywithoffset-d"));
        Assert.True(ev.Idx.ContainsKey("session-r"));
        Assert.Equal(11, ev.Idx.Count);
    }

    [Fact]
    public async Task SyncStackTagsAsync()
    {
        const string Tag1 = "Tag One";
        const string Tag2 = "Tag Two";
        const string Tag2_Lowercase = "tag two";

        var ev = GenerateEvent(DateTime.UtcNow);
        ev.Tags ??= [];
        ev.Tags.Add(Tag1);

        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);
        Assert.NotNull(ev.StackId);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId, o => o.Cache());
        Assert.Equal(new[] { Tag1 }, stack.Tags.ToArray());

        ev = _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.UtcNow);
        ev.Tags ??= [];
        ev.Tags.Add(Tag2);

        await RefreshDataAsync();
        context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        stack = await _stackRepository.GetByIdAsync(ev.StackId, o => o.Cache());
        Assert.Equal(new[] { Tag1, Tag2 }, stack.Tags.ToArray());

        ev = _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.UtcNow);
        ev.Tags ??= [];
        ev.Tags.Add(Tag2_Lowercase);

        await RefreshDataAsync();
        context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);
        stack = await _stackRepository.GetByIdAsync(ev.StackId, o => o.Cache());
        Assert.Equal(new[] { Tag1, Tag2 }, stack.Tags.ToArray());
    }

    [Fact]
    public async Task RemoveTagsExceedingLimitsWhileKeepingKnownTags()
    {
        string LargeRemovedTags = new('x', 150);

        var ev = GenerateEvent(DateTime.UtcNow);
        ev.Tags ??= [];
        ev.Tags.Add(LargeRemovedTags);

        var context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);
        Assert.NotNull(ev.StackId);
        Assert.NotNull(ev.Tags);
        Assert.Empty(ev.Tags);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId, o => o.Cache());
        Assert.Empty(stack.Tags);

        ev = _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.UtcNow);
        ev.Tags.AddRange(Enumerable.Range(0, 100).Select(i => i.ToString()));

        await RefreshDataAsync();
        context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);
        Assert.NotNull(ev.StackId);
        Assert.NotNull(ev.Tags);
        Assert.Equal(50, ev.Tags.Count);

        stack = await _stackRepository.GetByIdAsync(ev.StackId, o => o.Cache());
        Assert.Equal(50, stack.Tags.Count);

        ev = _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.UtcNow);
        ev.Tags ??= [];
        ev.Tags.Add(new string('x', 150));
        ev.Tags.AddRange(Enumerable.Range(100, 200).Select(i => i.ToString()));
        ev.Tags.Add(Event.KnownTags.Critical);

        await RefreshDataAsync();
        context = await _pipeline.RunAsync(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.False(context.HasError, context.ErrorMessage);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);
        Assert.NotNull(ev.StackId);
        Assert.NotNull(ev.Tags);
        Assert.Equal(50, ev.Tags.Count);
        Assert.DoesNotContain(new string('x', 150), ev.Tags);
        Assert.Contains(Event.KnownTags.Critical, ev.Tags);

        stack = await _stackRepository.GetByIdAsync(ev.StackId, o => o.Cache());
        Assert.Equal(50, stack.Tags.Count);
        Assert.DoesNotContain(new string('x', 150), stack.Tags);
        Assert.Contains(Event.KnownTags.Critical, stack.Tags);
    }

    [Fact]
    public async Task EnsureSingleNewStackAsync()
    {
        string source = Guid.NewGuid().ToString();
        var contexts = new List<EventContext> {
                new(new PersistentEvent { ProjectId = TestConstants.ProjectId, OrganizationId = TestConstants.OrganizationId, Message = "Test Sample", Source = source, Date = DateTime.UtcNow, Type = Event.KnownTypes.Log }, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject()),
                new(new PersistentEvent { ProjectId = TestConstants.ProjectId, OrganizationId = TestConstants.OrganizationId, Message = "Test Sample", Source = source, Date = DateTime.UtcNow, Type = Event.KnownTypes.Log }, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject()),
            };

        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.True(contexts.All(c => c.Stack?.Id == contexts.First().Stack?.Id));
        Assert.Equal(1, contexts.Count(c => c.IsNew));
        Assert.Equal(1, contexts.Count(c => !c.IsNew));
        Assert.Equal(2, contexts.Count(c => !c.IsRegression));
    }

    [Fact]
    public async Task EnsureSingleGlobalErrorStackAsync()
    {
        var contexts = new List<EventContext> {
                new(new PersistentEvent {
                    ProjectId = TestConstants.ProjectId,
                    OrganizationId = TestConstants.OrganizationId,
                    Message = "Test Exception",
                    Date = DateTime.UtcNow,
                    Type = Event.KnownTypes.Error,
                    Data = new DataDictionary { { "@error", new Error { Message = "Test Exception", Type = "Error" } } }
                }, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject()),
                new(new PersistentEvent {
                    ProjectId = TestConstants.ProjectId,
                    OrganizationId = TestConstants.OrganizationId,
                    Message = "Test Exception",
                    Date = DateTime.UtcNow,
                    Type = Event.KnownTypes.Error,
                    Data = new DataDictionary { { "@error", new Error { Message = "Test Exception 2", Type = "Error" } } }
                }, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject()),
            };

        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.True(contexts.All(c => c.Stack?.Id == contexts.First().Stack?.Id));
        Assert.Equal(1, contexts.Count(c => c.IsNew));
        Assert.Equal(1, contexts.Count(c => !c.IsNew));
        Assert.Equal(2, contexts.Count(c => !c.IsRegression));
    }

    [Fact]
    public async Task EnsureSingleRegressionAsync()
    {
        var utcNow = DateTime.UtcNow;
        var ev = _eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow);
        var context = new EventContext(ev, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        await _pipeline.RunAsync(context);

        Assert.False(context.HasError, context.ErrorMessage);
        Assert.True(context.IsProcessed);
        Assert.False(context.IsRegression);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        stack.MarkFixed(null, TimeProvider);
        await _stackRepository.SaveAsync(stack, o => o.Cache());

        var contexts = new List<EventContext> {
                new(_eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(-1)), _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject()),
                new(_eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(-1)), _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject())
            };

        await RefreshDataAsync();
        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.Equal(0, contexts.Count(c => c.IsRegression));
        Assert.Equal(2, contexts.Count(c => !c.IsRegression));

        contexts =
        [
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1)),
                _organizationData.GenerateSampleOrganization(_billingManager, _plans),
                _projectData.GenerateSampleProject()),
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1)),
                _organizationData.GenerateSampleOrganization(_billingManager, _plans),
                _projectData.GenerateSampleProject())
        ];

        await RefreshDataAsync();
        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.Equal(1, contexts.Count(c => c.IsRegression));
        Assert.Equal(1, contexts.Count(c => !c.IsRegression));

        contexts =
        [
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1)),
                _organizationData.GenerateSampleOrganization(_billingManager, _plans),
                _projectData.GenerateSampleProject()),
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1)),
                _organizationData.GenerateSampleOrganization(_billingManager, _plans),
                _projectData.GenerateSampleProject())
        ];

        await RefreshDataAsync();
        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.Equal(2, contexts.Count(c => !c.IsRegression));
    }

    [Fact]
    public async Task EnsureVersionedRegressionAsync()
    {
        var utcNow = DateTime.UtcNow;
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        var project = _projectData.GenerateSampleProject();
        var ev = _eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow);
        var context = new EventContext(ev, organization, project);
        await _pipeline.RunAsync(context);
        await RefreshDataAsync();

        Assert.False(context.HasError, context.ErrorMessage);
        Assert.True(context.IsProcessed);
        Assert.False(context.IsRegression);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        Assert.NotNull(ev);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        stack.MarkFixed(new SemanticVersion(1, 0, 1, ["rc2"]), TimeProvider);
        await _stackRepository.SaveAsync(stack, o => o.Cache());

        var contexts = new List<EventContext> {
                new(_eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1)), organization, project),
                new(_eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1), semver: "1.0.0"), organization, project),
                new(_eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1), semver: "1.0.0-beta2"), organization, project)
            };

        await RefreshDataAsync();
        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.Equal(0, contexts.Count(c => c.IsRegression));
        Assert.Equal(3, contexts.Count(c => !c.IsRegression));

        contexts =
        [
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1),
                    semver: "1.0.0"), organization, project),
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1),
                    semver: "1.0.1-rc1"), organization, project),
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(1),
                    semver: "1.0.1-rc3"), organization, project),
            new(
                _eventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId,
                    organizationId: TestConstants.OrganizationId, occurrenceDate: utcNow.AddMinutes(-1),
                    semver: "1.0.1-rc3"), organization, project)
        ];

        await RefreshDataAsync();
        await _pipeline.RunAsync(contexts);
        Assert.True(contexts.All(c => !c.HasError));
        Assert.Equal(1, contexts.Count(c => c.IsRegression));
        Assert.Equal(3, contexts.Count(c => !c.IsRegression));

        var regressedEvent = contexts.First(c => c.IsRegression).Event;
        Assert.Equal(utcNow.AddMinutes(-1), regressedEvent.Date);
        Assert.Equal("1.0.1-rc3", regressedEvent.GetVersion());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureIncludePrivateInformationIsRespectedAsync(bool includePrivateInformation)
    {
        var project = _projectData.GenerateSampleProject();
        project.Configuration.Settings.Add(SettingsDictionary.KnownKeys.IncludePrivateInformation, includePrivateInformation.ToString());

        var contexts = new List<EventContext> {
                new(new PersistentEvent {
                    ProjectId = TestConstants.ProjectId,
                    OrganizationId = TestConstants.OrganizationId,
                    Message = "Test Exception",
                    Date = DateTime.UtcNow,
                    Type = Event.KnownTypes.Error,
                    Data = new DataDictionary {
                        { "@error", new Error { Message = "Test Exception", Type = "Error" } },
                        { "@request", new RequestInfo { Path = "/test", ClientIpAddress = "127.0.0.1", Cookies = new Dictionary<string, string> {{ "test", "test" }}, PostData = "test", QueryString = new Dictionary<string, string> {{ "test", "test" }}} },
                        { "@environment", new EnvironmentInfo { IpAddress = "127.0.0.1", OSName = "Windows", MachineName = "Test" } },
                        { "@user", new UserInfo { Identity = "test@exceptionless.com" } },
                        { "@user_description", new UserDescription { EmailAddress = "test@exceptionless.com", Description = "test" } }
                    }
                }, _organizationData.GenerateSampleOrganization(_billingManager, _plans), project)
            };

        await _pipeline.RunAsync(contexts);
        var context = contexts.Single();
        Assert.False(context.HasError);

        var requestInfo = context.Event.GetRequestInfo();
        var environmentInfo = context.Event.GetEnvironmentInfo();
        var userInfo = context.Event.GetUserIdentity();
        var userDescription = context.Event.GetUserDescription();

        Assert.Equal("/test", requestInfo?.Path);
        Assert.Equal("Windows", environmentInfo?.OSName);
        Assert.Equal("test", userDescription?.Description);
        if (includePrivateInformation)
        {
            Assert.NotNull(requestInfo?.ClientIpAddress);
            Assert.NotNull(requestInfo.Cookies);
            Assert.Single(requestInfo.Cookies);
            Assert.NotNull(requestInfo.PostData);
            Assert.NotNull(requestInfo.QueryString);
            Assert.Single(requestInfo.QueryString);

            Assert.NotNull(environmentInfo?.IpAddress);
            Assert.NotNull(environmentInfo.MachineName);

            Assert.NotNull(userInfo?.Identity);
            Assert.NotNull(userDescription?.EmailAddress);
        }
        else
        {
            Assert.Null(requestInfo?.ClientIpAddress);
            Assert.NotNull(requestInfo?.Cookies);
            Assert.Empty(requestInfo.Cookies);
            Assert.Null(requestInfo.PostData);
            Assert.NotNull(requestInfo?.QueryString);
            Assert.Empty(requestInfo.QueryString);

            Assert.Null(environmentInfo?.IpAddress);
            Assert.Null(environmentInfo?.MachineName);

            Assert.Null(userInfo);
            Assert.Null(userDescription?.EmailAddress);
        }
    }

    [Fact]
    public async Task WillHandleDiscardedStack()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        var project = _projectData.GenerateSampleProject();

        var ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now);
        var context = await _pipeline.RunAsync(ev, organization, project);
        Assert.True(context.IsProcessed);
        Assert.False(context.HasError);
        Assert.False(context.IsCancelled);
        Assert.False(context.IsDiscarded);
        await RefreshDataAsync();

        var stack = context.Stack;
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Open, stack.Status);

        stack.Status = StackStatus.Discarded;
        stack = await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());

        ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now);
        context = await _pipeline.RunAsync(ev, organization, project);
        Assert.False(context.IsProcessed);
        Assert.False(context.HasError);
        Assert.True(context.IsCancelled);
        Assert.True(context.IsDiscarded);
        await RefreshDataAsync();

        ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now);
        context = await _pipeline.RunAsync(ev, organization, project);
        Assert.False(context.IsProcessed);
        Assert.False(context.HasError);
        Assert.True(context.IsCancelled);
        Assert.True(context.IsDiscarded);
    }

    [Theory]
    [InlineData(StackStatus.Regressed, false, null, null)]
    [InlineData(StackStatus.Fixed, true, "1.0.0", null)] // A fixed stack should not be marked as regressed if the event has no version.
    [InlineData(StackStatus.Regressed, false, null, "1.0.0")]
    [InlineData(StackStatus.Regressed, false, "1.0.0", "1.0.0")] // A fixed stack should not be marked as regressed if the event has the same version.
    [InlineData(StackStatus.Fixed, true, "2.0.0", "1.0.0")]
    [InlineData(StackStatus.Regressed, false, null, "1.0.1")]
    [InlineData(StackStatus.Regressed, false, "1.0.0", "1.0.1")]
    public async Task CanDiscardStackEventsBasedOnEventVersion(StackStatus expectedStatus, bool expectedDiscard, string? stackFixedInVersion, string? eventSemanticVersion)
    {
        var organization = await _organizationRepository.GetByIdAsync(TestConstants.OrganizationId, o => o.Cache());
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId, o => o.Cache());

        var ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now);
        var context = await _pipeline.RunAsync(ev, organization, project);

        var stack = context.Stack;
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Open, stack.Status);

        Assert.True(context.IsProcessed);
        Assert.False(context.HasError);
        Assert.False(context.IsCancelled);
        Assert.False(context.IsDiscarded);

        var semanticVersionParser = GetService<SemanticVersionParser>();
        var fixedInVersion = semanticVersionParser.Parse(stackFixedInVersion);
        stack.MarkFixed(fixedInVersion, TimeProvider);
        await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());

        await RefreshDataAsync();
        ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now, semver: eventSemanticVersion);
        context = await _pipeline.RunAsync(ev, organization, project);

        stack = context.Stack;
        Assert.NotNull(stack);
        Assert.Equal(expectedStatus, stack.Status);
        Assert.Equal(expectedDiscard, context.IsCancelled);
        Assert.Equal(expectedDiscard, context.IsDiscarded);
    }

    [Theory]
    [InlineData("1.0.0", null)] // A fixed stack should not be marked as regressed if the event has no version.
    [InlineData("2.0.0", "1.0.0")]
    public async Task WillNotDiscardStackEventsBasedOnEventVersionWithFreePlan(string stackFixedInVersion, string? eventSemanticVersion)
    {
        var organization = await _organizationRepository.GetByIdAsync(TestConstants.OrganizationId3, o => o.Cache());

        var plans = GetService<BillingPlans>();
        Assert.Equal(plans.FreePlan.Id, organization.PlanId);

        var project = await _projectRepository.AddAsync(_projectData.GenerateProject(organizationId: organization.Id), o => o.ImmediateConsistency().Cache());

        var ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now);
        var context = await _pipeline.RunAsync(ev, organization, project);

        var stack = context.Stack;
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Open, stack.Status);

        Assert.True(context.IsProcessed);
        Assert.False(context.HasError);
        Assert.False(context.IsCancelled);
        Assert.False(context.IsDiscarded);

        var semanticVersionParser = GetService<SemanticVersionParser>();
        var fixedInVersion = semanticVersionParser.Parse(stackFixedInVersion);
        stack.MarkFixed(fixedInVersion, TimeProvider);
        await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());

        await RefreshDataAsync();
        ev = _eventData.GenerateEvent(organizationId: organization.Id, projectId: project.Id, type: Event.KnownTypes.Log, source: "test", occurrenceDate: DateTimeOffset.Now, semver: eventSemanticVersion);
        context = await _pipeline.RunAsync(ev, organization, project);

        stack = context.Stack;
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Fixed, stack.Status);
        Assert.False(context.IsCancelled);
        Assert.False(context.IsDiscarded);
    }

    [Theory]
    [MemberData(nameof(Events))]
    public async Task ProcessEventsAsync(string errorFilePath)
    {
        var pipeline = GetService<EventPipeline>();
        var parserPluginManager = GetService<EventParserPluginManager>();
        var events = parserPluginManager.ParseEvents(await File.ReadAllTextAsync(errorFilePath), 2, "exceptionless/2.0.0.0");
        Assert.NotNull(events);
        Assert.True(events.Count > 0);

        foreach (var ev in events)
        {
            ev.Date = DateTime.UtcNow;
            ev.ProjectId = TestConstants.ProjectId;
            ev.OrganizationId = TestConstants.OrganizationId;
        }

        var contexts = await pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
        Assert.True(contexts.All(c => c.IsProcessed));
        Assert.True(contexts.All(c => !c.IsCancelled));
        Assert.True(contexts.All(c => !c.HasError));
    }

    [Fact]
    public async Task PipelinePerformanceAsync()
    {
        var parserPluginManager = GetService<EventParserPluginManager>();
        var pipeline = GetService<EventPipeline>();
        var startDate = DateTimeOffset.Now.SubtractHours(1);
        int totalBatches = 0;
        int totalEvents = 0;

        var sw = new Stopwatch();

        string path = Path.Combine("..", "..", "..", "Pipeline", "Data");
        foreach (string file in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories))
        {
            var events = parserPluginManager.ParseEvents(await File.ReadAllTextAsync(file), 2, "exceptionless/2.0.0.0");
            Assert.NotNull(events);
            Assert.True(events.Count > 0);

            foreach (var ev in events)
            {
                ev.Date = startDate;
                ev.ProjectId = TestConstants.ProjectId;
                ev.OrganizationId = TestConstants.OrganizationId;
            }

            sw.Start();
            var contexts = await pipeline.RunAsync(events, _organizationData.GenerateSampleOrganization(_billingManager, _plans), _projectData.GenerateSampleProject());
            sw.Stop();

            Assert.True(contexts.All(c => c.IsProcessed));
            Assert.True(contexts.All(c => !c.IsCancelled));
            Assert.True(contexts.All(c => !c.HasError));

            startDate = startDate.AddSeconds(5);
            totalBatches++;
            totalEvents += events.Count;
        }

        _logger.LogInformation("Took {Duration:g} to process {EventCount} with an average post size of {AveragePostSize}", sw.Elapsed, totalEvents, Math.Round(totalEvents * 1.0 / totalBatches, 4));
    }

    [Fact(Skip = "Used to create performance data from the queue directory")]
    public async Task GeneratePerformanceDataAsync()
    {
        int currentBatchCount = 0;
        var parserPluginManager = GetService<EventParserPluginManager>();
        string dataDirectory = Path.GetFullPath(Path.Combine("..", "..", "..", "Pipeline", "Data"));

        foreach (string file in Directory.GetFiles(dataDirectory))
            File.Delete(file);

        var mappedUsers = new Dictionary<string, UserInfo>();
        var mappedIPs = new Dictionary<string, string>();
        var storage = new FolderFileStorage(new FolderFileStorageOptions
        {
            Folder = Path.GetFullPath(Path.Combine("..", "..", "..", "src")),
            LoggerFactory = Log
        });

        foreach (var file in await storage.GetFileListAsync(Path.Combine("Exceptionless.Web", "storage", "q", "*")))
        {
            byte[] data = await storage.GetFileContentsRawAsync(Path.ChangeExtension(file.Path, ".payload"));
            var eventPostInfo = await storage.GetObjectAsync<EventPostInfo>(file.Path);
            if (!String.IsNullOrEmpty(eventPostInfo.ContentEncoding))
                data = data.Decompress(eventPostInfo.ContentEncoding);

            var encoding = Encoding.UTF8;
            if (!String.IsNullOrEmpty(eventPostInfo.CharSet))
                encoding = Encoding.GetEncoding(eventPostInfo.CharSet);

            string input = encoding.GetString(data);
            var events = parserPluginManager.ParseEvents(input, eventPostInfo.ApiVersion, eventPostInfo.UserAgent);

            foreach (var ev in events)
            {
                ev.Date = new DateTimeOffset(new DateTime(2020, 1, 1));
                ev.ProjectId = null!;
                ev.OrganizationId = null!;
                ev.StackId = null!;

                if (ev.Message is not null)
                    ev.Message = RandomData.GetSentence();

                ev.Data ??= new DataDictionary();
                var keysToRemove = ev.Data.Keys.Where(k => !k.StartsWith('@') && k != "MachineName" && k != "job" && k != "host" && k != "process").ToList();
                foreach (string key in keysToRemove)
                    ev.Data.Remove(key);

                ev.Data.Remove(Event.KnownDataKeys.UserDescription);
                var identity = ev.GetUserIdentity();
                if (identity?.Identity is not null)
                {
                    if (!mappedUsers.ContainsKey(identity.Identity))
                        mappedUsers.Add(identity.Identity, new UserInfo(Guid.NewGuid().ToString(), currentBatchCount.ToString()));

                    ev.SetUserIdentity(mappedUsers[identity.Identity]);
                }

                var request = ev.GetRequestInfo();
                if (request is not null)
                {
                    request.Cookies?.Clear();
                    request.PostData = null;
                    request.QueryString?.Clear();
                    request.Referrer = null;
                    request.Host = RandomData.GetIp4Address();
                    request.Path = $"/{RandomData.GetWord(false)}/{RandomData.GetWord(false)}";
                    request.Data?.Clear();

                    if (request.ClientIpAddress is not null)
                    {
                        if (!mappedIPs.ContainsKey(request.ClientIpAddress))
                            mappedIPs.Add(request.ClientIpAddress, RandomData.GetIp4Address());

                        request.ClientIpAddress = mappedIPs[request.ClientIpAddress];
                    }
                }

                InnerError? error = ev.GetError();
                while (error is not null)
                {
                    error.Message = RandomData.GetSentence();
                    error.Data?.Clear();
                    (error as Error)?.Modules.Clear();

                    error = error.Inner;
                }

                var environment = ev.GetEnvironmentInfo();
                environment?.Data?.Clear();
            }

            // inject random session start events.
            if (currentBatchCount % 10 == 0)
                events.Insert(0, events[0].ToSessionStartEvent());

            await storage.SaveObjectAsync(Path.Combine(dataDirectory, $"{currentBatchCount++}.json"), events);
        }
    }

    public static TheoryData<string> Events
    {
        get
        {
            var result = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "ErrorData"), "*.expected.json", SearchOption.AllDirectories))
                result.Add(file);

            return new TheoryData<string>(result);
        }
    }

    private async Task CreateProjectDataAsync(BillingPlan? plan = null)
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

            await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        }

        await _projectRepository.AddAsync(_projectData.GenerateSampleProjects(), o => o.ImmediateConsistency().Cache());

        foreach (var user in _userData.GenerateSampleUsers())
        {
            if (user.Id == TestConstants.UserId)
            {
                user.OrganizationIds.Add(TestConstants.OrganizationId2);
                user.OrganizationIds.Add(TestConstants.OrganizationId3);
            }

            if (!user.IsEmailAddressVerified)
                user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

            await _userRepository.AddAsync(user, o => o.ImmediateConsistency().Cache());
        }
    }

    private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string? userIdentity = null, string? type = null, string? sessionId = null)
    {
        occurrenceDate ??= DateTimeOffset.Now;
        return _eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
    }
}
