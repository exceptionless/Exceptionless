using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Utility;
using Nest;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Jobs {
    public class EventPostJobTests : ElasticTestBase {
        private readonly EventPostsJob _job;
        private readonly IFileStorage _storage;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _queue;
        private readonly UserRepository _userRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public EventPostJobTests(ITestOutputHelper output) : base(output) {
            _job = GetService<EventPostsJob>();
            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _storage = GetService<IFileStorage>();
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _eventRepository = GetService<IEventRepository>();
            _queue = GetService <IQueue<EventPost>>();
            _userRepository = GetService<UserRepository>();
            _jsonSerializerSettings = GetService<JsonSerializerSettings>();

            CreateDataAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanRunJob() {
            var ev = GenerateEvent();
            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(1, (await _storage.GetFileListAsync()).Count());

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
            var ev = GenerateEvent();
            for (int i = 1; i < 150; i++)
                ev.Data[$"{i}MB"] = new string('0', 1024 * 1000);

            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(1, (await _storage.GetFileListAsync()).Count());

            var result = await _job.RunAsync();
            Assert.False(result.IsSuccess);

            var stats = await _queue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Completed);
        }

        [Fact]
        public async Task CanRunJobWithNonExistingEventDataAsync() {
            var ev = GenerateEvent();
            Assert.NotNull(await EnqueueEventPostAsync(ev));
            Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Enqueued);

            var files = await _storage.GetFileListAsync();
            foreach (var file in files)
                await _storage.DeleteFileAsync(file.Path);

            var result = await _job.RunAsync();
            Assert.False(result.IsSuccess);

            var stats = await _queue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Abandoned);
        }

        private async Task CreateDataAsync() {
            foreach (Organization organization in OrganizationData.GenerateSampleOrganizations()) {
                if (organization.Id == TestConstants.OrganizationId3)
                    BillingManager.ApplyBillingPlan(organization, BillingManager.FreePlan, UserData.GenerateSampleUser());
                else
                    BillingManager.ApplyBillingPlan(organization, BillingManager.SmallPlan, UserData.GenerateSampleUser());

                organization.StripeCustomerId = Guid.NewGuid().ToString("N");
                organization.CardLast4 = "1234";
                organization.SubscribeDate = SystemClock.UtcNow;

                if (organization.IsSuspended) {
                    organization.SuspendedByUserId = TestConstants.UserId;
                    organization.SuspensionCode = SuspensionCode.Billing;
                    organization.SuspensionDate = SystemClock.UtcNow;
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

            await _configuration.Client.RefreshAsync(Indices.All);
        }

        private async Task<string> EnqueueEventPostAsync(PersistentEvent ev) {
            return await _queue.EnqueueAsync(new EventPostInfo {
                ApiVersion = 2,
                CharSet = "utf-8",
                ContentEncoding = "gzip",
                Data = await ev.GetBytes(_jsonSerializerSettings).CompressAsync(),
                MediaType = "application/json",
                ProjectId = ev.ProjectId,
                UserAgent = "exceptionless-test",
            }, _storage, false).AnyContext();
        }

        private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string userIdentity = null, string type = null, string sessionId = null) {
            if (!occurrenceDate.HasValue)
                occurrenceDate = SystemClock.OffsetNow;

            return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
        }
    }
}