using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Extensions;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.AsyncEx;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Api.Tests.Services {
    public sealed class UsageServiceTests : ElasticTestBase {
        private readonly ICacheClient _cache;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly UsageService _usageService;

        public UsageServiceTests(ITestOutputHelper output) : base(output) {
            _cache = GetService<ICacheClient>();
            _usageService = GetService<UsageService>();
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();

            Log.SetLogLevel<OrganizationRepository>(LogLevel.Information);
        }

        [Fact]
        public async Task CanIncrementUsageAsync() {
            var messageBus = GetService<IMessageBus>();

            var countdown = new AsyncCountdownEvent(2);
            await messageBus.SubscribeAsync<PlanOverage>(po => {
                _logger.LogInformation("Plan Overage for {organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
                countdown.Signal();
            });

            var o = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.SmallPlan.Id });
            var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = o.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, opt => opt.Cache());

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.InRange(o.GetHourlyEventLimit(), 1, 750);

            int totalToIncrement = o.GetHourlyEventLimit() - 1;
            Assert.False(await _usageService.IncrementUsageAsync(o, project, false, totalToIncrement));
            await _configuration.Client.RefreshAsync(Indices.All);
            o = await _organizationRepository.GetByIdAsync(o.Id);

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(2, countdown.CurrentCount);
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));

            Assert.True(await _usageService.IncrementUsageAsync(o, project, false, 2));
            await _configuration.Client.RefreshAsync(Indices.All);
            o = await _organizationRepository.GetByIdAsync(o.Id);

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(totalToIncrement + 2, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement + 2, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(totalToIncrement + 2, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement + 2, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(1, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(1, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(1, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(1, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));

            o = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.SmallPlan.Id });
            project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = o.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, opt => opt.Cache());
            await _configuration.Client.RefreshAsync(Indices.All);

            await _cache.RemoveAllAsync();
            totalToIncrement = o.GetHourlyEventLimit() + 20;
            Assert.True(await _usageService.IncrementUsageAsync(o, project, false, totalToIncrement));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(0, countdown.CurrentCount);
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(20, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(20, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(20, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(20, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));
        }


        [Fact]
        public async Task WillNotThrottleFreePlan() {
            var messageBus = GetService<IMessageBus>();

            var countdown = new AsyncCountdownEvent(2);
            await messageBus.SubscribeAsync<PlanOverage>(po => {
                _logger.LogInformation("Plan Overage for {organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
                countdown.Signal();
            });
            
            const int limit = 750;
            var o = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = limit, PlanId = BillingManager.FreePlan.Id });
            var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = o.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, opt => opt.Cache());

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(limit, o.GetHourlyEventLimit());

            Assert.False(await _usageService.IncrementUsageAsync(o, project, false, limit));
            await _configuration.Client.RefreshAsync(Indices.All);
            o = await _organizationRepository.GetByIdAsync(o.Id);

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(2, countdown.CurrentCount);
            Assert.Equal(limit, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(limit, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(limit, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(limit, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));

            Assert.True(await _usageService.IncrementUsageAsync(o, project, false, 2));
            await _configuration.Client.RefreshAsync(Indices.All);
            o = await _organizationRepository.GetByIdAsync(o.Id);

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(limit + 2, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(limit + 2, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(limit + 2, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(limit + 2, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(2, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(2, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(2, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(2, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));
        }

        [Fact]
        public async Task CanIncrementSuspendedOrganizationUsageAsync() {
            var messageBus = GetService<IMessageBus>();

            var countdown = new AsyncCountdownEvent(2);
            await messageBus.SubscribeAsync<PlanOverage>(po => {
                _logger.LogInformation("Plan Overage for {organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
                countdown.Signal();
            });

            var o = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.SmallPlan.Id }, opt => opt.Cache());
            var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = o.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, opt => opt.Cache());
            Assert.False(await _usageService.IncrementUsageAsync(o, project, false, 5));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(2, countdown.CurrentCount);
            Assert.Equal(5, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(5, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));

            o.IsSuspended = true;
            o.SuspendedByUserId = TestConstants.UserId;
            o.SuspensionDate = SystemClock.UtcNow;
            o.SuspensionCode = SuspensionCode.Billing;
            o = await _organizationRepository.SaveAsync(o, opt => opt.Cache());

            Assert.True(await _usageService.IncrementUsageAsync(o, project, false, 4995));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(5000, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5000, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(5000, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5000, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));

            o.RemoveSuspension();
            o = await _organizationRepository.SaveAsync(o, opt => opt.Cache());

            Assert.False(await _usageService.IncrementUsageAsync(o, project, false, 1));
            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(5001, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5001, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(5001, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5001, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id, project.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id, project.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id, project.Id), 0));
        }

        [Fact]
        public async Task RunBenchmarkAsync() {
            const int iterations = 10000;
            var org = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000000, PlanId = BillingManager.ExtraLargePlan.Id}, opt => opt.Cache());
            var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = org.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, opt => opt.Cache());

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                await _usageService.IncrementUsageAsync(org, project, false);

            sw.Stop();
            _logger.LogInformation("Time: {Duration:g}, Avg: ({AverageTickDuration:g}ticks | {AverageDuration}ms)", sw.Elapsed, sw.ElapsedTicks / iterations, sw.ElapsedMilliseconds / iterations);
        }

        private string GetHourlyBlockedCacheKey(string organizationId, string projectId = null) {
            string key = String.Concat("usage:blocked", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
            return projectId == null ? key : String.Concat(key, ":", projectId);
        }

        private string GetHourlyTotalCacheKey(string organizationId, string projectId = null) {
            string key = String.Concat("usage:total", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
            return projectId == null ? key : String.Concat(key, ":", projectId);
        }

        private string GetMonthlyBlockedCacheKey(string organizationId, string projectId = null) {
            string key = String.Concat("usage:blocked", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
            return projectId == null ? key : String.Concat(key, ":", projectId);
        }

        private string GetMonthlyTotalCacheKey(string organizationId, string projectId = null) {
            string key = String.Concat("usage:total", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
            return projectId == null ? key : String.Concat(key, ":", projectId);
        }
    }
}
