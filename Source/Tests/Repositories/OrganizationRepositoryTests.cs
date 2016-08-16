using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Extensions;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nito.AsyncEx;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class OrganizationRepositoryTests : ElasticRepositoryTestBase {
        public OrganizationRepositoryTests(ITestOutputHelper output) : base(output) {
            RemoveDataAsync().GetAwaiter().GetResult();
        }

        private OrganizationRepository GetRepository(IMessageBus messageBus = null) {
           return new OrganizationRepository(_configuration, IoC.GetInstance<IValidator<Organization>>(), _cache, messageBus, Log.CreateLogger<OrganizationRepository>());
        }

        [Fact]
        public async Task CanCreateUpdateRemoveAsync() {
            var repository = GetRepository();
            Assert.Equal(0, await repository.CountAsync());

            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            await repository.AddAsync(organization);
            await _client.RefreshAsync();
            Assert.NotNull(organization.Id);

            organization = await repository.GetByIdAsync(organization.Id);
            Assert.NotNull(organization);

            organization.Name = "New organization";
            await repository.SaveAsync(organization);

            await repository.RemoveAsync(organization.Id);
        }

        [Fact]
        public async Task CanFindManyAsync() {
            var repository = GetRepository();
            Assert.Equal(0, await repository.CountAsync());

            await repository.AddAsync(new[] {
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 0 },
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 1 },
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 2 }
            });

            await _client.RefreshAsync();
            Assert.Equal(3, await repository.CountAsync());

            var organizations = await repository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            var organizations2 = await repository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            Assert.NotEqual(organizations.Documents.First(), organizations2.Documents.First());

            organizations = await repository.GetByRetentionDaysEnabledAsync(new PagingOptions());
            Assert.NotNull(organizations);
            Assert.Equal(2, organizations.Total);

            await repository.RemoveAsync(organizations.Documents);
            await _client.RefreshAsync();

            Assert.Equal(1, await repository.CountAsync());
            await repository.RemoveAllAsync();
            await _client.RefreshAsync();
        }

        [Fact]
        public async Task CanAddAndGetByCachedAsync() {
            var repository = GetRepository();
            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            Assert.Equal(0, _cache.Count);
            await repository.AddAsync(organization, true);
            await _client.RefreshAsync();
            Assert.NotNull(organization.Id);
            Assert.Equal(1, _cache.Count);

            await _cache.RemoveAllAsync();
            Assert.Equal(0, _cache.Count);
            await repository.GetByIdAsync(organization.Id, true);
            Assert.NotNull(organization.Id);
            Assert.Equal(1, _cache.Count);

            await repository.RemoveAllAsync();
            await _client.RefreshAsync();
            Assert.Equal(0, _cache.Count);
        }

        [Fact]
        public async Task CanIncrementUsageAsync() {
            var messageBus = new InMemoryMessageBus(Log);
            var repository = GetRepository(messageBus);

            var countdown = new AsyncCountdownEvent(2);
            messageBus.Subscribe<PlanOverage>(po => {
                _logger.Info($"Plan Overage for {po.OrganizationId} (Hourly: {po.IsHourly})");
                countdown.Signal();
            });

            var o = await repository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.FreePlan.Id });
            await _client.RefreshAsync();
            Assert.InRange(o.GetHourlyEventLimit(), 1, 750);

            int totalToIncrement = o.GetHourlyEventLimit() - 1;
            Assert.False(await repository.IncrementUsageAsync(o.Id, false, totalToIncrement));
            await _client.RefreshAsync();
            o = await repository.GetByIdAsync(o.Id);

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(2, countdown.CurrentCount);
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            Assert.True(await repository.IncrementUsageAsync(o.Id, false, 2));
            await _client.RefreshAsync();
            o = await repository.GetByIdAsync(o.Id);
            
            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(totalToIncrement + 2, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement + 2, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(1, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(1, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            o = await repository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.FreePlan.Id });
            await _client.RefreshAsync();

            totalToIncrement = o.GetHourlyEventLimit() + 20;
            Assert.True(await repository.IncrementUsageAsync(o.Id, false, totalToIncrement));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(0, countdown.CurrentCount);
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(totalToIncrement, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(20, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(20, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
        }

        [Fact]
        public async Task CanIncrementSuspendedOrganizationUsageAsync() {
            var messageBus = new InMemoryMessageBus(Log);
            var repository = GetRepository(messageBus);

            var countdown = new AsyncCountdownEvent(2);
            messageBus.Subscribe<PlanOverage>(po => {
                _logger.Info($"Plan Overage for {po.OrganizationId} (Hourly: {po.IsHourly}");
                countdown.Signal();
            });

            var o = await repository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = BillingManager.FreePlan.Id }, true);
            Assert.False(await repository.IncrementUsageAsync(o.Id, false, 5));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(2, countdown.CurrentCount);
            Assert.Equal(5, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(0, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            o.IsSuspended = true;
            o.SuspendedByUserId = TestConstants.UserId;
            o.SuspensionDate = SystemClock.UtcNow;
            o.SuspensionCode = SuspensionCode.Billing;
            o = await repository.SaveAsync(o, true);
            
            Assert.True(await repository.IncrementUsageAsync(o.Id, false, 4995));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(5000, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5000, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));

            o.RemoveSuspension();
            o = await repository.SaveAsync(o, true);

            Assert.False(await repository.IncrementUsageAsync(o.Id, false, 1));

            await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(1, countdown.CurrentCount);
            Assert.Equal(5001, await _cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id), 0));
            Assert.Equal(5001, await _cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id), 0));
            Assert.Equal(4995, await _cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id), 0));
        }

        private string GetHourlyBlockedCacheKey(string organizationId) {
            return String.Concat("organization:usage-blocked", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTotalCacheKey(string organizationId) {
            return String.Concat("organization:usage-total", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetMonthlyBlockedCacheKey(string organizationId) {
            return String.Concat("organization:usage-blocked", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTotalCacheKey(string organizationId) {
            return String.Concat("organization:usage-total", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        }
    }
}