using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Logging.Xunit;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Nest;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Repositories {
    public class OrganizationRepositoryTests : TestWithLoggingBase {
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IOrganizationRepository _repository = IoC.GetInstance<IOrganizationRepository>();

        public OrganizationRepositoryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task CanCreateUpdateRemoveAsync() {
            await _client.RefreshAsync();
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
            Assert.Equal(0, await _repository.CountAsync());

            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            await _repository.AddAsync(organization);
            await _client.RefreshAsync();
            Assert.NotNull(organization.Id);

            organization = await _repository.GetByIdAsync(organization.Id);
            Assert.NotNull(organization);

            organization.Name = "New organization";
            await _repository.SaveAsync(organization);

            await _repository.RemoveAsync(organization.Id);
        }

        [Fact]
        public async Task CanFindManyAsync() {
            await _client.RefreshAsync();
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
            Assert.Equal(0, await _repository.CountAsync());

            await _repository.AddAsync(new[] {
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 0 },
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 1 },
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 2 }
            });

            await _client.RefreshAsync();
            Assert.Equal(3, await _repository.CountAsync());

            var organizations = await _repository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            var organizations2 = await _repository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            Assert.NotEqual(organizations.Documents.First(), organizations2.Documents.First());

            organizations = await _repository.GetByRetentionDaysEnabledAsync(new PagingOptions());
            Assert.NotNull(organizations);
            Assert.Equal(2, organizations.Total);

            await _repository.RemoveAsync(organizations.Documents);
            await _client.RefreshAsync();

            Assert.Equal(1, await _repository.CountAsync());
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
        }

        [Fact]
        public async Task CanAddAndGetByCachedAsync() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync();

            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            Assert.Equal(0, cache.Count);
            await _repository.AddAsync(organization, true);
            await _client.RefreshAsync();
            Assert.NotNull(organization.Id);
            Assert.Equal(1, cache.Count);

            await cache.RemoveAllAsync();
            Assert.Equal(0, cache.Count);
            await _repository.GetByIdAsync(organization.Id, true);
            Assert.NotNull(organization.Id);
            Assert.Equal(1, cache.Count);

            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task CanIncrementUsageAsync() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync();

            var messages = new List<PlanOverage>();
            var messagePublisher = IoC.GetInstance<IMessagePublisher>() as InMemoryMessageBus;
            Assert.NotNull(messagePublisher);
            messagePublisher.Subscribe<PlanOverage>(po => {
                _logger.Info($"Plan Overage for {po.OrganizationId} (Hourly: {po.IsHourly})");
                messages.Add(po);
            });

            var o = await _repository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.FreePlan.Id });
            await _client.RefreshAsync();
            Assert.InRange(o.GetHourlyEventLimit(), 1, 750);

            int totalToIncrement = o.GetHourlyEventLimit() - 1;
            Assert.False(await _repository.IncrementUsageAsync(o.Id, false, totalToIncrement));
            await _client.RefreshAsync();
            o = await _repository.GetByIdAsync(o.Id);
            Assert.Equal(0, messages.Count);
            Assert.Equal(totalToIncrement, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(0, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            Assert.True(await _repository.IncrementUsageAsync(o.Id, false, 2));
            await _client.RefreshAsync();
            o = await _repository.GetByIdAsync(o.Id);
            await Task.Delay(5);
            Assert.Equal(1, messages.Count);
            Assert.Equal(totalToIncrement + 2, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement + 2, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(1, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(1, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            o = await _repository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.FreePlan.Id });
            await _client.RefreshAsync();

            totalToIncrement = o.GetHourlyEventLimit() + 20;
            Assert.True(await _repository.IncrementUsageAsync(o.Id, false, totalToIncrement));
            await Task.Delay(5);
            Assert.Equal(2, messages.Count);
            Assert.Equal(totalToIncrement, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(20, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(20, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
        }

        [Fact]
        public async Task CanIncrementSuspendedOrganizationUsageAsync() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync();

            var messages = new List<PlanOverage>();
            var messagePublisher = IoC.GetInstance<IMessagePublisher>() as InMemoryMessageBus;
            Assert.NotNull(messagePublisher);
            messagePublisher.Subscribe<PlanOverage>(po => {
                _logger.Info($"Plan Overage for {po.OrganizationId} (Hourly: {po.IsHourly}");
                messages.Add(po);
            });

            var o = await _repository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.FreePlan.Id }, true);

            Assert.False(await _repository.IncrementUsageAsync(o.Id, false, 5));
            Assert.Equal(0, messages.Count);
            Assert.Equal(5, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(0, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            o.IsSuspended = true;
            o.SuspendedByUserId = TestConstants.UserId;
            o.SuspensionDate = DateTime.UtcNow;
            o.SuspensionCode = SuspensionCode.Billing;
            o = await _repository.SaveAsync(o, true);
            
            Assert.True(await _repository.IncrementUsageAsync(o.Id, false, 4995));
            Assert.Equal(0, messages.Count);
            Assert.Equal(5000, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5000, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(4995, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            o.RemoveSuspension();
            o = await _repository.SaveAsync(o, true);

            Assert.False(await _repository.IncrementUsageAsync(o.Id, false, 1));
            Assert.Equal(0, messages.Count);
            Assert.Equal(5001, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5001, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(4995, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
        }

        private string GetHourlyBlockedCacheKey(string organizationId) {
            return String.Concat("organization:usage-blocked", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTotalCacheKey(string organizationId) {
            return String.Concat("organization:usage-total", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetMonthlyBlockedCacheKey(string organizationId) {
            return String.Concat("organization:usage-blocked", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTotalCacheKey(string organizationId) {
            return String.Concat("organization:usage-total", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }
    }
}