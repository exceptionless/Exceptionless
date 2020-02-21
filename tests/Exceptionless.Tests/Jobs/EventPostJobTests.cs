using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Jobs {
    public class EventPostJobTests : IntegrationTestsBase {
        private readonly EventPostsJob _job;
        private readonly IFileStorage _storage;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IUserRepository _userRepository;
        private readonly ITextSerializer _serializer;
        private readonly EventPostService _eventPostService;
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;
        private readonly AppOptions _options;

        public EventPostJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _job = GetService<EventPostsJob>();
            _eventQueue = GetService <IQueue<EventPost>>();
            _storage = GetService<IFileStorage>();
            _eventPostService = new EventPostService(_eventQueue, _storage, Log);
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _eventRepository = GetService<IEventRepository>();
            _userRepository = GetService<IUserRepository>();
            _serializer = GetService<ITextSerializer>();
            _billingManager = GetService<BillingManager>();
            _plans = GetService<BillingPlans>();
            _options = GetService<AppOptions>();
        }
        
        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await _eventQueue.DeleteQueueAsync();
            await CreateDataAsync();
        }

        [Fact]
        public async Task CanRunJob() {
            var ev = GenerateEvent();
            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(2, (await _storage.GetFileListAsync()).Count);

            var result = await _job.RunAsync();
            Assert.True(result.IsSuccess);

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Completed);

            await RefreshDataAsync();
            Assert.Equal(1, await _eventRepository.CountAsync());
        }

        [Fact]
        public async Task CanRunJobWithMassiveEventAsync() {
            var ev = GenerateEvent();
            for (int i = 1; i < 150; i++)
                ev.Data[$"{i}MB"] = new string('0', 1024 * 1000);

            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(2, (await _storage.GetFileListAsync()).Count);

            var result = await _job.RunAsync();
            Assert.False(result.IsSuccess);

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Completed);
        }

        [Fact]
        public async Task CanRunJobWithNonExistingEventDataAsync() {
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

        private async Task CreateDataAsync() {
            foreach (var organization in OrganizationData.GenerateSampleOrganizations(_billingManager, _plans)) {
                if (organization.Id == TestConstants.OrganizationId3)
                    _billingManager.ApplyBillingPlan(organization, _plans.FreePlan, UserData.GenerateSampleUser());
                else
                    _billingManager.ApplyBillingPlan(organization, _plans.SmallPlan, UserData.GenerateSampleUser());

                organization.StripeCustomerId = Guid.NewGuid().ToString("N");
                organization.CardLast4 = "1234";
                organization.SubscribeDate = SystemClock.UtcNow;

                if (organization.IsSuspended) {
                    organization.SuspendedByUserId = TestConstants.UserId;
                    organization.SuspensionCode = SuspensionCode.Billing;
                    organization.SuspensionDate = SystemClock.UtcNow;
                }

                await _organizationRepository.AddAsync(organization, o => o.Cache().ImmediateConsistency());
            }

            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.Cache().ImmediateConsistency());

            foreach (var user in UserData.GenerateSampleUsers()) {
                if (user.Id == TestConstants.UserId) {
                    user.OrganizationIds.Add(TestConstants.OrganizationId2);
                    user.OrganizationIds.Add(TestConstants.OrganizationId3);
                }

                if (!user.IsEmailAddressVerified)
                    user.CreateVerifyEmailAddressToken();

                await _userRepository.AddAsync(user, o => o.Cache().ImmediateConsistency());
            }
        }

        private async Task<string> EnqueueEventPostAsync(PersistentEvent ev) {
            var eventPostInfo = new EventPost(_options.EnableArchive) {
                OrganizationId = ev.OrganizationId,
                ProjectId = ev.ProjectId,
                ApiVersion = 2,
                CharSet = "utf-8",
                ContentEncoding = "gzip",
                MediaType = "application/json",
                UserAgent = "exceptionless-test",
            };

            var stream = new MemoryStream(_serializer.SerializeToBytes(ev).Compress());
            return await _eventPostService.EnqueueAsync(eventPostInfo, stream).AnyContext();
        }

        private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string userIdentity = null, string type = null, string sessionId = null) {
            if (!occurrenceDate.HasValue)
                occurrenceDate = SystemClock.OffsetNow;

            return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
        }
    }
}