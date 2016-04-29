using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Tests.Utility;
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
using Foundatio.Logging.Xunit;
using Nest;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Jobs {
    public class CloseInactiveSessionsJobTests : TestWithLoggingBase {
        private readonly CloseInactiveSessionsJob _job = IoC.GetInstance<CloseInactiveSessionsJob>();
        private readonly ICacheClient _cacheClient = IoC.GetInstance<ICacheClient>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly ITokenRepository _tokenRepository = IoC.GetInstance<ITokenRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly EventPipeline _pipeline = IoC.GetInstance<EventPipeline>();

        public CloseInactiveSessionsJobTests(ITestOutputHelper output) : base(output) {}

        [Theory]
        [InlineData(1, true, null, false)]
        [InlineData(1, true, 70, false)]
        [InlineData(1, false, 50, false)]
        [InlineData(1, true, 50, true)]
        [InlineData(60, false, null, false)]
        public async Task CloseInactiveSessions(int defaultInactivePeriodInMinutes, bool willCloseSession, int? sessionHeartbeatUpdatedAgoInSeconds, bool heartbeatClosesSession) {
            await ResetAsync();

            const string userId = "blake@exceptionless.io";
            var ev = GenerateEvent(DateTimeOffset.Now.SubtractMinutes(5), userId);

            var context = await _pipeline.RunAsync(ev);
            Assert.False(context.HasError, context.ErrorMessage);
            Assert.False(context.IsCancelled);
            Assert.True(context.IsProcessed);

            await _client.RefreshAsync();
            var events = await _eventRepository.GetAllAsync();
            Assert.Equal(2, events.Total);
            Assert.Equal(1, events.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());
            var sessionStart = events.Documents.First(e => e.IsSessionStart());
            Assert.Equal(0, sessionStart.Value);
            Assert.False(sessionStart.HasSessionEndTime());

            var utcNow = DateTime.UtcNow;
            if (sessionHeartbeatUpdatedAgoInSeconds.HasValue) {
                var client = new ScopedCacheClient(_cacheClient, "session");
                await client.SetAsync($"project:{sessionStart.ProjectId}:heartbeat:{userId.ToSHA1()}", utcNow.SubtractSeconds(sessionHeartbeatUpdatedAgoInSeconds.Value));
                if (heartbeatClosesSession)
                    await client.SetAsync($"project:{sessionStart.ProjectId}:heartbeat:{userId.ToSHA1()}-close", true);
            }

            _job.DefaultInactivePeriod = TimeSpan.FromMinutes(defaultInactivePeriodInMinutes);
            Assert.Equal(JobResult.Success, await _job.RunAsync());
            await _client.RefreshAsync();
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

            await _client.RefreshAsync();
        }

        private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null, string userIdentity = null, string type = null, string sessionId = null) {
            if (!occurrenceDate.HasValue)
                occurrenceDate = DateTimeOffset.Now;

            return EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, generateTags: false, generateData: false, occurrenceDate: occurrenceDate, userIdentity: userIdentity, type: type, sessionId: sessionId);
        }

        private async Task RemoveDataAsync() {
            await RemoveEventsAndStacks();
            await _tokenRepository.RemoveAllAsync();
            await _cacheClient.RemoveAllAsync();
            await _userRepository.RemoveAllAsync();
            await _cacheClient.RemoveAllAsync();
            await _projectRepository.RemoveAllAsync();
            await _cacheClient.RemoveAllAsync();
            await _organizationRepository.RemoveAllAsync();
            await _client.RefreshAsync();
            await _cacheClient.RemoveAllAsync();
        }

        private async Task RemoveEventsAndStacks() {
            await _client.RefreshAsync();
            await _eventRepository.RemoveAllAsync();
            await _client.RefreshAsync();
            await _stackRepository.RemoveAllAsync();
            await _client.RefreshAsync();
            await _cacheClient.RemoveAllAsync();
        }
    }
}
