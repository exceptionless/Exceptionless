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
using Foundatio.Storage;
using Nest;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Pipeline {
    public class EventPipelineTests : CaptureTests, IDisposable {
        private readonly ICacheClient _cacheClient = IoC.GetInstance<ICacheClient>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly ITokenRepository _tokenRepository = IoC.GetInstance<ITokenRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();

        public EventPipelineTests(CaptureFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task NoFutureEventsAsync() {
            await ResetAsync();

            var localTime = DateTime.Now;
            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: localTime.AddMinutes(10));

            var pipeline = IoC.GetInstance<EventPipeline>();
            await pipeline.RunAsync(ev);
            await _client.RefreshAsync();

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.True(ev.Date < localTime.AddMinutes(10));
            Assert.True(ev.Date - localTime < TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task CanIndexExtendedDataAsync() {
            await ResetAsync();

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

            var pipeline = IoC.GetInstance<EventPipeline>();
            await pipeline.RunAsync(ev);
            await _client.RefreshAsync();

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

            var pipeline = IoC.GetInstance<EventPipeline>();
            await pipeline.RunAsync(ev);
            await _client.RefreshAsync();

            ev = await _eventRepository.GetByIdAsync(ev.Id);
            Assert.NotNull(ev);
            Assert.NotNull(ev.StackId);

            var stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag2);

            await pipeline.RunAsync(ev);
            await _client.RefreshAsync();

            stack = await _stackRepository.GetByIdAsync(ev.StackId, true);
            Assert.Equal(new TagSet { Tag1, Tag2 }, stack.Tags);

            ev = EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, occurrenceDate: DateTime.Now);
            ev.Tags.Add(Tag2_Lowercase);

            await pipeline.RunAsync(ev);
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

            var pipeline = IoC.GetInstance<EventPipeline>();
            await pipeline.RunAsync(contexts);
            await _client.RefreshAsync();
            Assert.True(contexts.All(c => c.Stack.Id == contexts.First().Stack.Id));
            Assert.Equal(1, contexts.Count(c => c.IsNew));
            Assert.Equal(1, contexts.Count(c => !c.IsNew));
            Assert.Equal(2, contexts.Count(c => !c.IsRegression));
        }

        [Fact]
        public async Task EnsureSingleGlobalErrorStackAsync() {
            await ResetAsync();

            var pipeline = IoC.GetInstance<EventPipeline>();

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

            await pipeline.RunAsync(contexts);
            await _client.RefreshAsync();

            Assert.True(contexts.All(c => c.Stack.Id == contexts.First().Stack.Id));
            Assert.Equal(1, contexts.Count(c => c.IsNew));
            Assert.Equal(1, contexts.Count(c => !c.IsNew));
            Assert.Equal(2, contexts.Count(c => !c.IsRegression));
        }

        [Fact]
        public async Task EnsureSingleRegressionAsync() {
            await ResetAsync();

            var pipeline = IoC.GetInstance<EventPipeline>();

            PersistentEvent ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow);
            var context = new EventContext(ev);
            await pipeline.RunAsync(context);
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

            await pipeline.RunAsync(contexts);
            await _client.RefreshAsync();
            Assert.Equal(1, contexts.Count(c => c.IsRegression));
            Assert.Equal(1, contexts.Count(c => !c.IsRegression));

            contexts = new List<EventContext> {
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1))),
                new EventContext(EventData.GenerateEvent(stackId: ev.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, occurrenceDate: DateTime.UtcNow.AddMinutes(1)))
            };

            await pipeline.RunAsync(contexts);
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

            var pipeline = IoC.GetInstance<EventPipeline>();
            foreach (var ev in events) {
                ev.Date = DateTime.UtcNow;
                ev.ProjectId = TestConstants.ProjectId;
                ev.OrganizationId = TestConstants.OrganizationId;

                var context = new EventContext(ev);
                await  pipeline.RunAsync(context);
                Assert.True(context.IsProcessed);
            }
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

            _writer.WriteLine($"Took {sw.ElapsedMilliseconds}ms to process {totalEvents} with an average post size of {Math.Round(totalEvents * 1.0/totalBatches, 4)}");
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
                    events.Insert(0, CreateSessionStartEvent(events[0]));

                await storage.SaveObjectAsync($"{dataDirectory}\\{currentBatchCount++}.json", events);
            }
        }

        private PersistentEvent CreateSessionStartEvent(PersistentEvent ev) {
            return new PersistentEvent {
                SessionId = ev.SessionId,
                Data = ev.Data,
                Date = ev.Date,
                Geo = ev.Geo,
                OrganizationId = ev.OrganizationId,
                ProjectId = ev.ProjectId,
                Tags = ev.Tags,
                Type = Event.KnownTypes.SessionStart
            };
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
            await _cacheClient.RemoveAllAsync();
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
