using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Pipeline {
    public class EventPipelineTests : IDisposable {
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly ITokenRepository _tokenRepository = IoC.GetInstance<ITokenRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly ICacheClient _cacheClient = IoC.GetInstance<ICacheClient>();

        private readonly EventPipeline _pipeline = IoC.GetInstance<EventPipeline>();

        [Fact]
        public async Task NoFutureEventsAsync() {
            await ResetAsync();

            var localTime = DateTime.Now;
            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: localTime.AddMinutes(10));

            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync();

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.True(ev.Date < localTime.AddMinutes(10));
            Assert.True(ev.Date - localTime < TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task AutoSessionAsync() {
            await ResetAsync();
            
            var ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTimeOffset.Now, userIdentity: "blake@exceptionless.io");

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);
            
            await _client.RefreshAsync();
            var events = await _eventRepository.GetAllAsync();
            Assert.Equal(2, events.Total);
            Assert.Equal(1, events.Documents.Count(e => e.IsSessionStart()));
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.SessionId)).Select(e => e.SessionId).Distinct().Count());
        }
        
        [Fact]
        public async Task NoAutoSessionWithoutIdentityAsync() {
            await ResetAsync();

            var ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTimeOffset.Now);

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync();
            var events = await _eventRepository.GetAllAsync();
            Assert.Equal(1, events.Total);
            Assert.Equal(0, events.Documents.Count(e => e.IsSessionStart()));
            Assert.Equal(0, events.Documents.Where(e => !String.IsNullOrEmpty(e.SessionId)).Select(e => e.SessionId).Distinct().Count());
        }

        [Fact]
        public async Task UpdateSessionLastActivityAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            DateTimeOffset lastEventDate = firstEventDate.Add(TimeSpan.FromMinutes(1));

            var events = new List<PersistentEvent> {
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate, userIdentity: "blake@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate.AddSeconds(10), userIdentity: "blake@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: lastEventDate, userIdentity: "blake@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: lastEventDate),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate, userIdentity: "eric@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate.AddSeconds(30), userIdentity: "eric@exceptionless.io", type: Event.KnownTypes.SessionStart),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: lastEventDate, userIdentity: "eric@exceptionless.io"),
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync();
            var results = await _eventRepository.GetAllAsync();
            Assert.Equal(10, results.Total);
            Assert.Equal(3, results.Documents.Where(e => !String.IsNullOrEmpty(e.SessionId)).Select(e => e.SessionId).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd() && e.GetUserIdentity()?.Identity == "eric@exceptionless.io"));
            Assert.Equal(2, results.Documents.Where(e => !String.IsNullOrEmpty(e.SessionId) && e.GetUserIdentity().Identity == "eric@exceptionless.io").Select(e => e.SessionId).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => String.IsNullOrEmpty(e.SessionId)));

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(3, sessionStarts.Count);

            var firstUserSessionStartEvents = sessionStarts.First(e => e.GetUserIdentity().Identity == "blake@exceptionless.io");
            Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, firstUserSessionStartEvents.Value);

            var secondUserSessionStartEvents = sessionStarts.Where(e => e.GetUserIdentity().Identity == "eric@exceptionless.io").OrderBy(e => e.Date).ToList();
            Assert.Equal(2, secondUserSessionStartEvents.Count);
            Assert.Equal(30, secondUserSessionStartEvents[0].Value);
            Assert.Null(secondUserSessionStartEvents[1].Value);
        }

        [Fact]
        public async Task WillCreateSessionStartEventAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            DateTimeOffset lastEventDate = firstEventDate.Add(TimeSpan.FromMinutes(1));

            var events = new List<PersistentEvent> {
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate, userIdentity: "blake@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate.AddSeconds(10),  type: Event.KnownTypes.SessionEnd, userIdentity: "blake@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate.AddSeconds(20), userIdentity: "blake@exceptionless.io"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate.AddSeconds(30),  type: Event.KnownTypes.SessionEnd, userIdentity: "blake@exceptionless.io"),
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync();
            var results = await _eventRepository.GetAllAsync();
            Assert.Equal(6, results.Total);
            Assert.Equal(2, results.Documents.Where(e => !String.IsNullOrEmpty(e.SessionId)).Select(e => e.SessionId).Distinct().Count());
            Assert.Equal(2, results.Documents.Count(e => e.IsSessionEnd()));

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(2, sessionStarts.Count);
            foreach (var sessionStart in sessionStarts) {
                Assert.Equal(10, sessionStart.Value);
                Assert.True(sessionStart.Data.ContainsKey(Event.KnownDataKeys.SessionEnd));
            }
        }

        [Fact]
        public async Task UpdateManualSessionLastActivityAsync() {
            await ResetAsync();

            DateTimeOffset firstEventDate = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(5));
            DateTimeOffset lastEventDate = firstEventDate.Add(TimeSpan.FromMinutes(1));

            var events = new List<PersistentEvent> {
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate, type: Event.KnownTypes.SessionStart, sessionId: "12345678"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: firstEventDate.AddSeconds(10), sessionId: "12345678"),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: lastEventDate, type: Event.KnownTypes.SessionEnd, sessionId: "12345678")
            };

            var contexts = await _pipeline.RunAsync(events);
            Assert.False(contexts.Any(c => c.HasError));
            Assert.False(contexts.Any(c => c.IsCancelled));
            Assert.True(contexts.Any(c => c.IsProcessed));

            await _client.RefreshAsync();
            var results = await _eventRepository.GetAllAsync();
            Assert.Equal(3, results.Total);
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionStart()));
            Assert.Equal(1, results.Documents.Where(e => !String.IsNullOrEmpty(e.SessionId)).Select(e => e.SessionId).Distinct().Count());
            Assert.Equal(1, results.Documents.Count(e => e.IsSessionEnd()));
            Assert.Equal((decimal)(lastEventDate - firstEventDate).TotalSeconds, results.Documents.First(e => e.IsSessionStart()).Value);
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
            
            ev.CopyDataToIndex();

            Assert.Equal(11, ev.Idx.Count);
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
        }

        [Fact]
        public async Task SyncStackTagsAsync() {
            await ResetAsync();

            const string Tag1 = "Tag One";
            const string Tag2 = "Tag Two";
            const string Tag2_Lowercase = "tag two";

            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag1);
            
            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync();

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.NotNull(ev.StackId);

            var stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag2);

            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync();

            stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag2_Lowercase);

            await _pipeline.RunAsync(ev);
            await _client.RefreshAsync();

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
            await _client.RefreshAsync();
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
            await _client.RefreshAsync();

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
            await _client.RefreshAsync();

            Assert.True(context.IsProcessed);
            Assert.False(context.IsRegression);

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);

            var stack = await _stackRepository.GetByIdAsync(ev.StackId);
            stack.DateFixed = DateTime.UtcNow;
            stack.IsRegressed = false;
            await _stackRepository.SaveAsync(stack);
            await _client.RefreshAsync();

            var contexts = new List<EventContext> {
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1))),
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1)))
            };

            await _pipeline.RunAsync(contexts);
            await _client.RefreshAsync();
            Assert.Equal(1, contexts.Count(c => c.IsRegression));
            Assert.Equal(1, contexts.Count(c => !c.IsRegression));

            contexts = new List<EventContext> {
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1))),
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1)))
            };

            await _pipeline.RunAsync(contexts);
            await _client.RefreshAsync();
            Assert.Equal(2, contexts.Count(c => !c.IsRegression));
        }

        [Theory]
        [MemberData("Events")]
        public async Task ProcessEventsAsync(string errorFilePath) {
            await ResetAsync();

            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            var events = parserPluginManager.ParseEvents(File.ReadAllText(errorFilePath), 2, "exceptionless/2.0.0.0");
            Assert.NotNull(events);
            Assert.True(events.Count > 0);
            
            foreach (var ev in events) {
                ev.Date = DateTime.UtcNow;
                ev.ProjectId = TestConstants.ProjectId;
                ev.OrganizationId = TestConstants.OrganizationId;

                var context = new EventContext(ev);
                await _pipeline.RunAsync(context);
                await _client.RefreshAsync();
                Assert.True(context.IsProcessed);
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

        private bool _isReset;
        private async Task ResetAsync() {
            if (!_isReset) {
                _isReset = true;
                await RemoveDataAsync();
                await CreateDataAsync();
            } else {
                await RemoveEventsAndStacks();
            }

            await _cacheClient.RemoveAllAsync();
        }

        private async Task CreateDataAsync() {
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
                    organization.SuspensionDate = DateTime.Now;
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

            await _client.RefreshAsync();
        }

        private async Task RemoveDataAsync() {
            await RemoveEventsAndStacks();
            await _tokenRepository.RemoveAllAsync();
            await _userRepository.RemoveAllAsync();
            await _projectRepository.RemoveAllAsync();
            await _organizationRepository.RemoveAllAsync();
            await _client.RefreshAsync();
        }

        private async Task RemoveEventsAndStacks() {
            await _eventRepository.RemoveAllAsync();
            await _client.RefreshAsync();
            await _stackRepository.RemoveAllAsync();
            await _client.RefreshAsync();
        }

        public async void Dispose() {
            await RemoveDataAsync();
        }
    }
}
