using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Jobs {
    public class CloseInactiveSessionsJobTests : ElasticTestBase {
        private readonly CloseInactiveSessionsJob _job;
        private readonly ICacheClient _cacheClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly UserRepository _userRepository;
        private readonly EventPipeline _pipeline;

        public CloseInactiveSessionsJobTests(ITestOutputHelper output) : base(output) {
            _job = GetService<CloseInactiveSessionsJob>();
            _cacheClient = GetService<ICacheClient>();
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _eventRepository = GetService<IEventRepository>();
            _userRepository = GetService<UserRepository>();
            _pipeline = GetService<EventPipeline>();

            CreateDataAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData(1, true, null, false)]
        [InlineData(1, true, 70, false)]
        [InlineData(1, false, 50, false)]
        [InlineData(1, true, 50, true)]
        [InlineData(60, false, null, false)]
        public async Task CloseInactiveSessions(int defaultInactivePeriodInMinutes, bool willCloseSession, int? sessionHeartbeatUpdatedAgoInSeconds, bool heartbeatClosesSession) {
            const string userId = "blake@exceptionless.io";
            var ev = GenerateEvent(SystemClock.OffsetNow.SubtractMinutes(5), userId);

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _configuration.Client.RefreshAsync();
            var events = await _eventRepository.GetAllAsync();
            Assert.Equal(2, events.Total);
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            var sessionStart = events.Documents.First(e => e.IsSessionStart());
            Assert.Equal(0, sessionStart.Value);
            Assert.False(sessionStart.HasSessionEndTime());

            var utcNow = SystemClock.UtcNow;
            if (sessionHeartbeatUpdatedAgoInSeconds.HasValue) {
                await _cacheClient.SetAsync($"project:{sessionStart.ProjectId}:heartbeat:{userId.ToSHA1()}", utcNow.SubtractSeconds(sessionHeartbeatUpdatedAgoInSeconds.Value));
                if (heartbeatClosesSession)
                    await _cacheClient.SetAsync($"project:{sessionStart.ProjectId}:heartbeat:{userId.ToSHA1()}-close", true);
            }

            _job.DefaultInactivePeriod = TimeSpan.FromMinutes(defaultInactivePeriodInMinutes);
            Assert.Equal(JobResult.Success, await _job.RunAsync());
            await _configuration.Client.RefreshAsync();
            events = await _eventRepository.GetAllAsync();
            Assert.Equal(2, events.Total);

            sessionStart = events.Documents.First(e => e.IsSessionStart());
            decimal sessionStartDuration = (decimal)(sessionHeartbeatUpdatedAgoInSeconds.HasValue ? (utcNow.SubtractSeconds(sessionHeartbeatUpdatedAgoInSeconds.Value) - sessionStart.Date.UtcDateTime).TotalSeconds : 0);
            if (willCloseSession) {
                Assert.Equal(sessionStartDuration, sessionStart.Value);
                Assert.True(sessionStart.HasSessionEndTime());
            } else {
                Assert.Equal(sessionStartDuration, sessionStart.Value);
                Assert.False(sessionStart.HasSessionEndTime());
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

            await _configuration.Client.RefreshAsync();
        }

        private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string userIdentity = null, string type = null, string sessionId = null) {
            if (!occurrenceDate.HasValue)
                occurrenceDate = SystemClock.OffsetNow;

            return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
        }
    }
}
