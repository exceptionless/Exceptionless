using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Logging.Xunit;
using Foundatio.Repositories.Models;
using Foundatio.Storage;
using Nest;
using Xunit;
using Xunit.Abstractions;

using Fields = Exceptionless.Core.Repositories.Configuration.EventIndex.Fields.PersistentEvent;

namespace Exceptionless.Api.Tests.Pipeline {
    public class EventPipelineTests : TestWithLoggingBase {
        private readonly ICacheClient _cacheClient = IoC.GetInstance<ICacheClient>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly ITokenRepository _tokenRepository = IoC.GetInstance<ITokenRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();

        private readonly EventPipeline _pipeline = IoC.GetInstance<EventPipeline>();

        public EventPipelineTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task NoFutureEventsAsync() {
            await ResetAsync();

            var localTime = DateTime.Now;
            var ev = GenerateEvent(localTime.AddMinutes(10));

            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync(Indices.All);

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.True(ev.Date < localTime.AddMinutes(10));
            Assert.True(ev.Date - localTime < TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task CreateAutoSessionAsync() {
            await CreateAutoSessionInternalAsync(DateTimeOffset.Now);
        }

        private async Task CreateAutoSessionInternalAsync(DateTimeOffset date) {
            await ResetAsync();

            var ev = GenerateEvent(date, "blake@exceptionless.io");

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync(Indices.All);
            var events = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(2, events.Total);
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStart = events.Documents.First(e => e.IsSessionStart());
            Assert.Equal(0, sessionStart.Value);
            Assert.False(sessionStart.HasSessionEndTime());
        }

        [Fact]
        public async Task CanUpdateExistingAutoSessionAsync() {
            var startDate = DateTimeOffset.Now.SubtractMinutes(5);
            await CreateAutoSessionInternalAsync(startDate);

            var ev = GenerateEvent(startDate.AddMinutes(4), "blake@exceptionless.io");

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync(Indices.All);
            var events = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(3, events.Total);
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStart = events.Documents.Single(e => e.IsSessionStart());
            Assert.Equal(240, sessionStart.Value);
            Assert.False(sessionStart.HasSessionEndTime());
        }

        [Fact]
        public async Task IgnoreAutoSessionsWithoutIdentityAsync() {
            await ResetAsync();

            var ev = GenerateEvent(DateTimeOffset.Now);

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync(Indices.All);
            var events = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(1, events.Total);
            Assert.Equal(0, events.Documents.Count(e => e.IsSessionStart()));
            Assert.Equal(0, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
        }
        
        [Fact]
        public async Task CreateAutoSessionStartEventsAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
                GenerateEvent(firstEventDate.AddSeconds(20), "blake@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(30), "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(6, results.Total);
            Assert.Equal(2, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(2, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(2, sessionStarts.Count);
            foreach (var sessionStart in sessionStarts) {
                Assert.Equal(10, sessionStart.Value);
                Assert.True(sessionStart.HasSessionEndTime());
            }
        }

        [Fact]
        public async Task UpdateAutoMultipleSessionStartEventDurationsAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io", Event.KnownTypes.Session),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.Session),
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(1, contexts.Count(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(1, results.Total);
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(0, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStart = results.Documents.Single(e => e.IsSessionStart());
            Assert.Equal(10, sessionStart.Value);
            Assert.False(sessionStart.HasSessionEndTime());
        }

        [Fact]
        public async Task UpdateAutoSessionLastActivityAsync() {
            await ResetAsync();

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

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(paging: new PagingOptions().WithLimit(15));
            Assert.Equal(9, results.Total);
            Assert.Equal(2, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd() && e.GetUserIdentity()?.Identity == "blake@exceptionless.io"));
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId()) && e.GetUserIdentity().Identity == "eric@exceptionless.io").Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => String.IsNullOrEmpty(e.GetSessionId())));
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(2, sessionStarts.Count);

            var firstUserSessionStartEvents = sessionStarts.Single(e => e.GetUserIdentity().Identity == "blake@exceptionless.io");
            Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, firstUserSessionStartEvents.Value);
            Assert.True(firstUserSessionStartEvents.HasSessionEndTime());
            
            var secondUserSessionStartEvents = sessionStarts.Single(e => e.GetUserIdentity().Identity == "eric@exceptionless.io");
            Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, secondUserSessionStartEvents.Value);
            Assert.False(secondUserSessionStartEvents.HasSessionEndTime());
        }

        [Fact]
        public async Task CloseExistingAutoSessionAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io"),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionHeartbeat)
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));
            await _client.RefreshAsync(Indices.All);
            
            events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.Session),
                GenerateEvent(firstEventDate.AddSeconds(20), "blake@exceptionless.io", Event.KnownTypes.SessionEnd)
            };

            contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(1, contexts.Count(c => c.IsCancelled));
            Assert.Equal(1, contexts.Count(c => c.IsProcessed));
            await _client.RefreshAsync(Indices.All);

            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(4, results.Total);
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStart = results.Documents.Single(e => e.IsSessionStart());
            Assert.Equal(20, sessionStart.Value);
            Assert.True(sessionStart.HasSessionEndTime());
        }

        [Fact]
        public async Task IgnoreDuplicateAutoEndSessionsAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, "blake@exceptionless.io", Event.KnownTypes.SessionEnd),
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionEnd)
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(2, contexts.Count(c => c.IsCancelled));
            Assert.False(contexts.All(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(0, results.Total);
        }

        [Fact]
        public async Task WillMarkAutoSessionHeartbeatStackHidden() {
            await ResetAsync();

            var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate.AddSeconds(10), "blake@exceptionless.io", Event.KnownTypes.SessionHeartbeat)
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(0, contexts.Count(c => c.IsCancelled));
            Assert.Equal(1, contexts.Count(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(2, results.Total);
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));

            var sessionHeartbeat = results.Documents.Single(e => e.IsSessionHeartbeat());
            Assert.NotNull(sessionHeartbeat);
            Assert.True(sessionHeartbeat.IsHidden);

            var stack = await _stackRepository.GetByIdAsync(sessionHeartbeat.StackId);
            Assert.NotNull(stack);
            Assert.True(stack.IsHidden);

            stack = await _stackRepository.GetByIdAsync(results.Documents.First(e => !e.IsSessionHeartbeat()).StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsHidden);
        }

        [Fact]
        public async Task CreateManualSessionAsync() {
            await CreateManualSessionInternalAsync(DateTimeOffset.Now);
        }

        private async Task CreateManualSessionInternalAsync(DateTimeOffset start) {
            await ResetAsync();

            var ev = GenerateEvent(start, sessionId: "12345678");

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync(Indices.All);
            var events = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(2, events.Total);
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStartEvent = events.Documents.SingleOrDefault(e => e.IsSessionStart());
            Assert.NotNull(sessionStartEvent);
            Assert.Equal(0, sessionStartEvent.Value);
            Assert.False(sessionStartEvent.HasSessionEndTime());
        }

        [Fact]
        public async Task CanUpdateExistingManualSessionAsync() {
            var startDate = DateTimeOffset.Now.SubtractMinutes(5);
            await CreateManualSessionInternalAsync(startDate);

            var ev = GenerateEvent(startDate.AddMinutes(4), sessionId: "12345678");

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync(Indices.All);
            var events = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(3, events.Total);
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStart = events.Documents.First(e => e.IsSessionStart());
            Assert.Equal(240, sessionStart.Value);
            Assert.False(sessionStart.HasSessionEndTime());
        }

        [Fact]
        public async Task CreateManualSingleSessionStartEventAsync() {
            await ResetAsync();

            var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.Session, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(20), type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(30), sessionId: "12345678"),
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(4, results.Total);
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStartEvent = results.Documents.SingleOrDefault(e => e.IsSessionStart());
            Assert.NotNull(sessionStartEvent);
            Assert.Equal(30, sessionStartEvent.Value);
            Assert.True(sessionStartEvent.HasSessionEndTime());
        }
        
        [Fact]
        public async Task CreateManualSessionStartEventAsync() {
            await ResetAsync();

            var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, sessionId: "12345678"),
                // This event will be deduplicated as part of the manual session plugin.
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(20), sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(30), type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(1, contexts.Count(c => c.IsCancelled));
            Assert.Equal(3, contexts.Count(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(4, results.Total);
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStartEvent = results.Documents.First(e => e.IsSessionStart());
            Assert.NotNull(sessionStartEvent);
            Assert.Equal(30, sessionStartEvent.Value);
            Assert.True(sessionStartEvent.HasSessionEndTime());
        }

        [Fact]
        public async Task UpdateManualSessionLastActivityAsync() {
            await ResetAsync();

            var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            var lastEventDate = firstEventDate.Add(TimeSpan.FromMinutes(1));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, type: Event.KnownTypes.Session, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), sessionId: "12345678"),
                GenerateEvent(lastEventDate, type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(3, results.Total);
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));
            Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, results.Documents.First(e => e.IsSessionStart()).Value);
        }
        
        [Fact]
        public async Task CloseExistingManualSessionAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionHeartbeat, sessionId: "12345678")
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));
            await _client.RefreshAsync(Indices.All);
            
            events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.Session, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(20), type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
            };

            contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(1, contexts.Count(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));
            await _client.RefreshAsync(Indices.All);

            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(4, results.Total);
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(1, sessionStarts.Count);
            foreach (var sessionStart in sessionStarts) {
                Assert.Equal(20, sessionStart.Value);
                Assert.True(sessionStart.HasSessionEndTime());
            }
        }

        [Fact]
        public async Task IgnoreDuplicateManualEndSessionsAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate, type: Event.KnownTypes.SessionEnd, sessionId: "12345678"),
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(2, contexts.Count(c => c.IsCancelled));
            Assert.False(contexts.All(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(0, results.Total);
        }

        [Fact]
        public async Task WillMarkManualSessionHeartbeatStackHidden() {
            await ResetAsync();

            var firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));

            var events = new List<PersistentEvent> {
                GenerateEvent(firstEventDate.AddSeconds(10), type: Event.KnownTypes.SessionHeartbeat, sessionId: "12345678")
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.Equal(0, contexts.Count(c => c.IsCancelled));
            Assert.Equal(1, contexts.Count(c => c.IsProcessed));

            await _client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(Fields.Date));
            Assert.Equal(2, results.Total);
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));

            var sessionHeartbeat = results.Documents.Single(e => e.IsSessionHeartbeat());
            Assert.NotNull(sessionHeartbeat);
            Assert.True(sessionHeartbeat.IsHidden);

            var stack = await _stackRepository.GetByIdAsync(sessionHeartbeat.StackId);
            Assert.NotNull(stack);
            Assert.True(stack.IsHidden);
            
            stack = await _stackRepository.GetByIdAsync(results.Documents.First(e => !e.IsSessionHeartbeat()).StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsHidden);
        }
        
        [Fact]
        public void CanIndexExtendedData() {
            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: DateTime.Now);
            ev.Data.Add("First Name", "Eric");
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
            
            ev.CopyDataToIndex();

            Assert.Equal(12, ev.Idx.Count);
            Assert.True(ev.Idx.ContainsKey("first-name-s"));
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
        }

        [Fact]
        public async Task SyncStackTagsAsync() {
            await ResetAsync();

            const string Tag1 = "Tag One";
            const string Tag2 = "Tag Two";
            const string Tag2_Lowercase = "tag two";

            PersistentEvent ev = GenerateEvent(DateTime.Now);
            ev.Tags.Add(Tag1);
            
            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync(Indices.All);

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.NotNull(ev.StackId);

            var stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag2);

            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync(Indices.All);

            stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag2_Lowercase);

            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync(Indices.All);

            stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);
        }

        [Fact]
        public async Task EnsureSingleNewStackAsync() {
            await ResetAsync();

            string source = Guid.NewGuid().ToString();
            var contexts = new List<EventContext> {
                new EventContext(new PersistentEvent { ProjectId = TestConstants.ProjectId, OrganizationId = TestConstants.OrganizationId, Message = "Test Sample", Source = source, Date = DateTime.UtcNow, Type = Event.KnownTypes.Log }),
                new EventContext(new PersistentEvent { ProjectId = TestConstants.ProjectId, OrganizationId = TestConstants.OrganizationId, Message = "Test Sample", Source = source, Date = DateTime.UtcNow, Type = Event.KnownTypes.Log}),
            };
            
            await _pipeline.RunAsync(contexts);
            await _client.RefreshAsync(Indices.All);
            Assert.True(contexts.All(c => c.Stack.Id == contexts.First().Stack.Id));
            Assert.Equal(1, contexts.Count(c => c.IsNew));
            Assert.Equal(1, contexts.Count(c => !c.IsNew));
            Assert.Equal(2, contexts.Count(c => !c.IsRegression));
        }

        [Fact]
        public async Task EnsureSingleGlobalErrorStackAsync() {
            await ResetAsync();
            
            var contexts = new List<EventContext> {
                new EventContext(new PersistentEvent {
                    ProjectId = TestConstants.ProjectId,
                    OrganizationId = TestConstants.OrganizationId,
                    Message = "Test Exception",
                    Date = DateTime.UtcNow,
                    Type = Event.KnownTypes.Error,
                    Data = new DataDictionary { { "@error", new Error { Message = "Test Exception", Type = "Error" } } }
                }),
                new EventContext(new PersistentEvent {
                    ProjectId = TestConstants.ProjectId,
                    OrganizationId = TestConstants.OrganizationId,
                    Message = "Test Exception",
                    Date = DateTime.UtcNow,
                    Type = Event.KnownTypes.Error,
                    Data = new DataDictionary { { "@error", new Error { Message = "Test Exception 2", Type = "Error" } } }
                }),
            };

            await _pipeline.RunAsync(contexts);
            await _client.RefreshAsync(Indices.All);

            Assert.True(contexts.All(c => c.Stack.Id == contexts.First().Stack.Id));
            Assert.Equal(1, contexts.Count(c => c.IsNew));
            Assert.Equal(1, contexts.Count(c => !c.IsNew));
            Assert.Equal(2, contexts.Count(c => !c.IsRegression));
        }

        [Fact]
        public async Task EnsureSingleRegressionAsync() {
            await ResetAsync();
            
            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow);
            var context = new EventContext(ev);
            await _pipeline.RunAsync(context);
            await _client.RefreshAsync(Indices.All);

            Assert.True(context.IsProcessed);
            Assert.False(context.IsRegression);

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);

            var stack = await _stackRepository.GetByIdAsync(ev.StackId);
            stack.DateFixed = DateTime.UtcNow;
            stack.IsRegressed = false;
            await _stackRepository.SaveAsync(stack, true);
            await _client.RefreshAsync(Indices.All);

            var contexts = new List<EventContext> {
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1))),
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1)))
            };

            await _pipeline.RunAsync(contexts);
            await _client.RefreshAsync(Indices.All);
            Assert.Equal(1, contexts.Count(c => c.IsRegression));
            Assert.Equal(1, contexts.Count(c => !c.IsRegression));

            contexts = new List<EventContext> {
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1))),
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1)))
            };

            await _pipeline.RunAsync(contexts);
            await _client.RefreshAsync(Indices.All);
            Assert.Equal(2, contexts.Count(c => !c.IsRegression));
        }

        private bool _canResetDataForProcessEvents = true;

        [Theory]
        [MemberData("Events")]
        public async Task ProcessEventsAsync(string errorFilePath) {
            if (_canResetDataForProcessEvents) {
                _canResetDataForProcessEvents = false;
                await ResetAsync();
            }

            var pipeline = IoC.GetInstance<EventPipeline>();
            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            var events = parserPluginManager.ParseEvents(File.ReadAllText(errorFilePath), 2, "exceptionless/2.0.0.0");
            Assert.NotNull(events);
            Assert.True(events.Count > 0);
            
            foreach (var ev in events) {
                ev.Date = DateTime.UtcNow;
                ev.ProjectId = TestConstants.ProjectId;
                ev.OrganizationId = TestConstants.OrganizationId;
            }
            
            var contexts = await pipeline.RunAsync(events);
            Assert.True(contexts.All(c => c.IsProcessed));
            Assert.True(contexts.All(c => !c.IsCancelled));
            Assert.True(contexts.All(c => !c.HasError));
        }

        [Fact]
        public async Task PipelinePerformance() {
            await ResetAsync();

            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            var pipeline = IoC.GetInstance<EventPipeline>();
            var startDate = DateTimeOffset.Now.SubtractHours(1);
            var totalBatches = 0;
            var totalEvents = 0;

            var sw = new Stopwatch();
            foreach (var file in Directory.GetFiles(@"..\..\Pipeline\Data\", "*.json", SearchOption.AllDirectories)) {
                var events = parserPluginManager.ParseEvents(File.ReadAllText(file), 2, "exceptionless/2.0.0.0");
                Assert.NotNull(events);
                Assert.True(events.Count > 0);

                foreach (var ev in events) {
                    ev.Date = startDate;
                    ev.ProjectId = TestConstants.ProjectId;
                    ev.OrganizationId = TestConstants.OrganizationId;
                }
                
                sw.Start();
                var contexts = await pipeline.RunAsync(events);
                sw.Stop();

                Assert.True(contexts.All(c => c.IsProcessed));
                Assert.True(contexts.All(c => !c.IsCancelled));
                Assert.True(contexts.All(c => !c.HasError));

                startDate = startDate.AddSeconds(5);
                totalBatches++;
                totalEvents += events.Count;
            }

            _logger.Info($"Took {sw.ElapsedMilliseconds}ms to process {totalEvents} with an average post size of {Math.Round(totalEvents * 1.0/totalBatches, 4)}");
        }

        [Fact(Skip = "Used to create performance data from the queue directory")]
        public async Task GeneratePerformanceData() {
            var currentBatchCount = 0;
            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            var dataDirectory = Path.GetFullPath(@"..\..\Pipeline\Data\");
            
            foreach (var file in Directory.GetFiles(dataDirectory))
                File.Delete(file);
            
            Dictionary<string, UserInfo> _mappedUsers = new Dictionary<string, UserInfo>();
            Dictionary<string, string> _mappedIPs = new Dictionary<string, string>();

            var storage = new FolderFileStorage(Path.GetFullPath(@"..\..\..\"));
            foreach (var file in await storage.GetFileListAsync(@"Api\App_Data\storage\q\*")) {
                var eventPostInfo = await storage.GetObjectAsync<EventPostInfo>(file.Path);
                byte[] data = eventPostInfo.Data;
                if (!String.IsNullOrEmpty(eventPostInfo.ContentEncoding))
                    data = data.Decompress(eventPostInfo.ContentEncoding);

                var encoding = Encoding.UTF8;
                if (!String.IsNullOrEmpty(eventPostInfo.CharSet))
                    encoding = Encoding.GetEncoding(eventPostInfo.CharSet);

                string input = encoding.GetString(data);
                var events = parserPluginManager.ParseEvents(input, eventPostInfo.ApiVersion, eventPostInfo.UserAgent);
                
                foreach (var ev in events) {
                    ev.Date = new DateTimeOffset(new DateTime(2020, 1, 1));
                    ev.ProjectId = null;
                    ev.OrganizationId = null;
                    ev.StackId = null;

                    if (ev.Message != null)
                        ev.Message = RandomData.GetSentence();

                    var keysToRemove = ev.Data.Keys.Where(k => !k.StartsWith("@") && k != "MachineName" && k != "job" && k != "host" && k != "process").ToList();
                    foreach (var key in keysToRemove)
                        ev.Data.Remove(key);

                    ev.Data.Remove(Event.KnownDataKeys.UserDescription);
                    var identity = ev.GetUserIdentity();
                    if (identity != null) {
                        if (!_mappedUsers.ContainsKey(identity.Identity))
                            _mappedUsers.Add(identity.Identity, new UserInfo(Guid.NewGuid().ToString(), currentBatchCount.ToString()));

                        ev.SetUserIdentity(_mappedUsers[identity.Identity]);
                    }
                    
                    var request = ev.GetRequestInfo();
                    if (request != null) {
                        request.Cookies?.Clear();
                        request.PostData = null;
                        request.QueryString?.Clear();
                        request.Referrer = null;
                        request.Host = RandomData.GetIp4Address();
                        request.Path = $"/{RandomData.GetWord(false)}/{RandomData.GetWord(false)}";
                        request.Data.Clear();

                        if (request.ClientIpAddress != null) {
                            if (!_mappedIPs.ContainsKey(request.ClientIpAddress))
                                _mappedIPs.Add(request.ClientIpAddress, RandomData.GetIp4Address());

                            request.ClientIpAddress = _mappedIPs[request.ClientIpAddress];
                        }
                    }

                    InnerError error = ev.GetError();
                    while (error != null) {
                        error.Message = RandomData.GetSentence();
                        error.Data.Clear();
                        (error as Error)?.Modules.Clear();

                        error = error.Inner;
                    }

                    var environment = ev.GetEnvironmentInfo();
                    environment?.Data.Clear();
                }
                
                // inject random session start events.
                if (currentBatchCount % 10 == 0)
                    events.Insert(0, events[0].ToSessionStartEvent());

                await storage.SaveObjectAsync($"{dataDirectory}\\{currentBatchCount++}.json", events);
            }
        }
        
        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "*.expected.json", SearchOption.AllDirectories))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }

        private static bool _isReset;
        private async Task ResetAsync() {
            if (!_isReset) {
                _isReset = true;
                await RemoveDataAsync();
                await CreateDataAsync();
            } else {
                await RemoveEventsAndStacks();
            }
        }

        private async Task CreateDataAsync() {
            await _client.RefreshAsync(Indices.All);

            foreach (Organization organization in OrganizationData.GenerateSampleOrganizations()) {
                if (organization.Id == TestConstants.OrganizationId3)
                    BillingManager.ApplyBillingPlan(organization, BillingManager.FreePlan, UserData.GenerateSampleUser());
                else
                    BillingManager.ApplyBillingPlan(organization, BillingManager.SmallPlan, UserData.GenerateSampleUser());

                organization.StripeCustomerId = Guid.NewGuid().ToString("N");
                organization.CardLast4 = "1234";
                organization.SubscribeDate = DateTime.Now;

                if (organization.IsSuspended) {
                    organization.SuspendedByUserId = TestConstants.UserId;
                    organization.SuspensionCode = SuspensionCode.Billing;
                    organization.SuspensionDate = DateTime.UtcNow;
                }

                await _organizationRepository.AddAsync(organization, true);
            }

            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), true);

            foreach (User user in UserData.GenerateSampleUsers()) {
                if (user.Id == TestConstants.UserId) {
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);
                    user.OrganizationIds.Add(TestConstants.OrganizationId3);
                }

                if (!user.IsEmailAddressVerified)
                    user.CreateVerifyEmailAddressToken();

                await _userRepository.AddAsync(user, true);
            }

            await _client.RefreshAsync(Indices.All);
        }

        private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string userIdentity = null, string type = null, string sessionId = null) {
            if (!occurrenceDate.HasValue)
                occurrenceDate = DateTimeOffset.Now;

            return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
        }

        private async Task RemoveDataAsync() {
            await RemoveEventsAndStacks();
            await _tokenRepository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
            await _userRepository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
            await _projectRepository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
            await _organizationRepository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
            await _cacheClient.RemoveAllAsync();
        }

        private async Task RemoveEventsAndStacks() {
            await _client.RefreshAsync(Indices.All);
            await _eventRepository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
            await _stackRepository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
            await _cacheClient.RemoveAllAsync();
        }
    }
}
