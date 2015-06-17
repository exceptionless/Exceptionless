using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Messaging;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class OrganizationRepositoryTests {
        public readonly IOrganizationRepository _repository = IoC.GetInstance<IOrganizationRepository>();

        [Fact]
        public void CanCreateUpdateRemove() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            _repository.Add(organization);
            Assert.NotNull(organization.Id);
            
            organization = _repository.GetById(organization.Id);
            Assert.NotNull(organization);

            organization.Name = "New organization";
            _repository.Save(organization);

            _repository.Remove(organization.Id);
        }

        [Fact]
        public void CanFindMany() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            _repository.Add(new[] {
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 0 }, 
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 1 }, 
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 2 }
            });

            var organizations = _repository.GetByRetentionDaysEnabled(new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Total);

            var organizations2 = _repository.GetByRetentionDaysEnabled(new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Total);

            Assert.NotEqual(organizations.Documents.First(), organizations2.Documents.First());
           
            organizations = _repository.GetByRetentionDaysEnabled(new PagingOptions());
            Assert.NotNull(organizations);
            Assert.Equal(2, organizations.Total);

            _repository.Remove(organizations.Documents);
            Assert.Equal(1, _repository.Count());
            _repository.RemoveAll();
        }
        
        [Fact]
        public void CanAddAndGetByCached() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            cache.FlushAll();
            
            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            Assert.Equal(0, cache.Count);
            _repository.Add(organization, true);
            Assert.NotNull(organization.Id);
            Assert.Equal(1, cache.Count);

            cache.FlushAll();
            Assert.Equal(0, cache.Count);
            _repository.GetById(organization.Id, true);
            Assert.NotNull(organization.Id);
            Assert.Equal(1, cache.Count);

            _repository.RemoveAll();
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void CanIncrementUsage() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            cache.FlushAll();

            var messages = new List<PlanOverage>();
            var messagePublisher = IoC.GetInstance<IMessagePublisher>() as InMemoryMessageBus;
            Assert.NotNull(messagePublisher);
            messagePublisher.Subscribe<PlanOverage>(messages.Add);

            var o = _repository.Add(new Organization {
                Name = "Test",
                MaxEventsPerMonth = 750,
                PlanId = BillingManager.FreePlan.Id
            });

            Assert.False(_repository.IncrementUsage(o.Id, false, 4));
            Assert.Equal(0, messages.Count);
            Assert.Equal(4, cache.Get<long>(GetHourlyTotalCacheKey(o.Id)));
            Assert.Equal(4, cache.Get<long>(GetMonthlyTotalCacheKey(o.Id)));
            Assert.Equal(0, cache.Get<long>(GetHourlyBlockedCacheKey(o.Id)));
            Assert.Equal(0, cache.Get<long>(GetMonthlyBlockedCacheKey(o.Id)));

            Assert.True(_repository.IncrementUsage(o.Id, false, 3));
            Assert.Equal(1, messages.Count);
            Assert.Equal(7, cache.Get<long>(GetHourlyTotalCacheKey(o.Id)));
            Assert.Equal(7, cache.Get<long>(GetMonthlyTotalCacheKey(o.Id)));
            Assert.Equal(1, cache.Get<long>(GetHourlyBlockedCacheKey(o.Id)));
            Assert.Equal(1, cache.Get<long>(GetMonthlyBlockedCacheKey(o.Id)));

            o = _repository.Add(new Organization {
                Name = "Test",
                MaxEventsPerMonth = 750,
                PlanId = BillingManager.FreePlan.Id
            });
            Assert.True(_repository.IncrementUsage(o.Id, false, 751));
            //Assert.Equal(2, messages.Count);
            Assert.Equal(751, cache.Get<long>(GetHourlyTotalCacheKey(o.Id)));
            Assert.Equal(751, cache.Get<long>(GetMonthlyTotalCacheKey(o.Id)));
            Assert.Equal(745, cache.Get<long>(GetHourlyBlockedCacheKey(o.Id)));
            Assert.Equal(745, cache.Get<long>(GetMonthlyBlockedCacheKey(o.Id)));
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