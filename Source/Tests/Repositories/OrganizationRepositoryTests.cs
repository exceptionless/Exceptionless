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
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class OrganizationRepositoryTests {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        public readonly IOrganizationRepository _repository = IoC.GetInstance<IOrganizationRepository>();

        [Fact]
        public async Task CanCreateUpdateRemove() {
            await _client.RefreshAsync().AnyContext();
            await _repository.RemoveAllAsync().AnyContext();
            await _client.RefreshAsync().AnyContext();
            Assert.Equal(0, await _repository.CountAsync().AnyContext());

            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            await _repository.AddAsync(organization).AnyContext();
            await _client.RefreshAsync().AnyContext();
            Assert.NotNull(organization.Id);
            
            organization = await _repository.GetByIdAsync(organization.Id).AnyContext();
            Assert.NotNull(organization);

            organization.Name = "New organization";
            await _repository.SaveAsync(organization).AnyContext();

            await _repository.RemoveAsync(organization.Id).AnyContext();
        }

        [Fact]
        public async Task CanFindMany() {
            await _client.RefreshAsync().AnyContext();
            await _repository.RemoveAllAsync().AnyContext();
            await _client.RefreshAsync().AnyContext();
            Assert.Equal(0, await _repository.CountAsync().AnyContext());

            await _repository.AddAsync(new[] {
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 0 }, 
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 1 }, 
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 2 }
            }).AnyContext();

            await _client.RefreshAsync().AnyContext();
            Assert.Equal(3, await _repository.CountAsync().AnyContext());

            var organizations = await _repository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(1).WithLimit(1)).AnyContext();
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            var organizations2 = await _repository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(2).WithLimit(1)).AnyContext();
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            Assert.NotEqual(organizations.Documents.First(), organizations2.Documents.First());
           
            organizations = await _repository.GetByRetentionDaysEnabledAsync(new PagingOptions()).AnyContext();
            Assert.NotNull(organizations);
            Assert.Equal(2, organizations.Total);

            await _repository.RemoveAsync(organizations.Documents).AnyContext();
            await _client.RefreshAsync().AnyContext();

            Assert.Equal(1, await _repository.CountAsync().AnyContext());
            await _repository.RemoveAllAsync().AnyContext();
        }
        
        [Fact]
        public async Task CanAddAndGetByCached() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync().AnyContext();
            
            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            Assert.Equal(0, cache.Count);
            await _repository.AddAsync(organization, true).AnyContext();
            Assert.NotNull(organization.Id);
            Assert.Equal(1, cache.Count);

            await cache.RemoveAllAsync().AnyContext();
            Assert.Equal(0, cache.Count);
            await _repository.GetByIdAsync(organization.Id, true).AnyContext();
            Assert.NotNull(organization.Id);
            Assert.Equal(1, cache.Count);

            await _repository.RemoveAllAsync().AnyContext();
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task CanIncrementUsage() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync().AnyContext();

            var messages = new List<PlanOverage>();
            var messagePublisher = IoC.GetInstance<IMessagePublisher>() as InMemoryMessageBus;
            Assert.NotNull(messagePublisher);
            messagePublisher.Subscribe<PlanOverage>(message => messages.Add(message));

            var o = await _repository.AddAsync(new Organization {
                Name = "Test",
                MaxEventsPerMonth = 750,
                PlanId = BillingManager.FreePlan.Id
            }).AnyContext();

            Assert.False(await _repository.IncrementUsageAsync(o.Id, false, 4).AnyContext());
            Assert.Equal(0, messages.Count);
            Assert.Equal(4, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id)).AnyContext());
            Assert.Equal(4, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id)).AnyContext());
            Assert.Equal(0, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id)).AnyContext());
            Assert.Equal(0, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id)).AnyContext());

            Assert.True(await _repository.IncrementUsageAsync(o.Id, false, 3).AnyContext());
            Assert.Equal(1, messages.Count);
            Assert.Equal(7, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id)).AnyContext());
            Assert.Equal(7, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id)).AnyContext());
            Assert.Equal(1, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id)).AnyContext());
            Assert.Equal(1, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id)).AnyContext());

            o = await _repository.AddAsync(new Organization {
                Name = "Test",
                MaxEventsPerMonth = 750,
                PlanId = BillingManager.FreePlan.Id
            }).AnyContext();

            Assert.True(await _repository.IncrementUsageAsync(o.Id, false, 751).AnyContext());
            //Assert.Equal(2, messages.Count);
            Assert.Equal(751, await cache.GetAsync<long>(GetHourlyTotalCacheKey(o.Id)).AnyContext());
            Assert.Equal(751, await cache.GetAsync<long>(GetMonthlyTotalCacheKey(o.Id)).AnyContext());
            Assert.Equal(745, await cache.GetAsync<long>(GetHourlyBlockedCacheKey(o.Id)).AnyContext());
            Assert.Equal(745, await cache.GetAsync<long>(GetMonthlyBlockedCacheKey(o.Id)).AnyContext());
        }

        private string GetHourlyBlockedCacheKey(string organizationId) {
            return String.Concat("usage-blocked", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTotalCacheKey(string organizationId) {
            return String.Concat("usage-total", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetMonthlyBlockedCacheKey(string organizationId) {
            return String.Concat("usage-blocked", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTotalCacheKey(string organizationId) {
            return String.Concat("usage-total", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetUsageSavedCacheKey(string organizationId) {
            return String.Concat("usage-saved", ":", organizationId);
        }
    }
}