using System;
using System.IO;
using System.Linq;
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
using Foundatio.Hosting.Startup;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Jobs {
    public class EventPostJobTests : ElasticTestBase {
        private readonly EventPostsJob _job;
        private readonly IFileStorage _storage;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _queue;
        private readonly IUserRepository _userRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly EventPostService _eventPostService;
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;
        private readonly IOptions<AppOptions> _options;

        public EventPostJobTests(ITestOutputHelper output) : base(output) {
            _job = GetService<EventPostsJob>();
            _queue = GetService <IQueue<EventPost>>();
            _storage = GetService<IFileStorage>();
            _eventPostService = new EventPostService(_queue, _storage, Log);
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _eventRepository = GetService<IEventRepository>();
            _userRepository = GetService<IUserRepository>();
            _jsonSerializerSettings = GetService<JsonSerializerSettings>();
            _billingManager = GetService<BillingManager>();
            _plans = GetService<BillingPlans>();
            _options = GetService<IOptions<AppOptions>>();
        }

        protected override void RegisterServices(IServiceCollection services) {
            base.RegisterServices(services);
            services.AddStartupAction("CreateDataAsync", CreateDataAsync);
        }

        [Fact]
        public async Task CanRunJob() {
            await _storage.DeleteFilesAsync(await _storage.GetFileListAsync());

            var ev = GenerateEvent();
            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(2, (await _storage.GetFileListAsync()).Count());

            var result = await _job.RunAsync();
            Assert.True(result.IsSuccess);

            var stats = await _queue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Completed);

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(1, await _eventRepository.CountAsync());
        }

        [Fact]
        public async Task CanRunJobWithMassiveEventAsync() {
            await _storage.DeleteFilesAsync(await _storage.GetFileListAsync());

            var ev = GenerateEvent();
            for (int i = 1; i < 150; i++)
                ev.Data[$"{i}MB"] = new string('0', 1024 * 1000);

            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(2, (await _storage.GetFileListAsync()).Count());

            var result = await _job.RunAsync();
            Assert.False(result.IsSuccess);

            var stats = await _queue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Completed);
        }

        [Fact]
        public async Task CanRunJobWithNonExistingEventDataAsync() {
            await _storage.DeleteFilesAsync(await _storage.GetFileListAsync());

            var ev = GenerateEvent();
            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Enqueued);

            await _storage.DeleteFilesAsync(await _storage.GetFileListAsync());

            var result = await _job.RunAsync();
            Assert.False(result.IsSuccess);

            var stats = await _queue.GetQueueStatsAsync();
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
            var eventPostInfo = new EventPost(_options.Value.EnableArchive) {
                OrganizationId = ev.OrganizationId,
                ProjectId = ev.ProjectId,
                ApiVersion = 2,
                CharSet = "utf-8",
                ContentEncoding = "gzip",
                MediaType = "application/json",
                UserAgent = "exceptionless-test",
            };

            var stream = new MemoryStream(ev.GetBytes(_jsonSerializerSettings).Compress());
            return await _eventPostService.EnqueueAsync(eventPostInfo, stream).AnyContext();
        }

        private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string userIdentity = null, string type = null, string sessionId = null) {
            if (!occurrenceDate.HasValue)
                occurrenceDate = SystemClock.OffsetNow;

            return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
        }
    }
}