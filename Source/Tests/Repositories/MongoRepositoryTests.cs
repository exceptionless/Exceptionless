using System;
using System.Linq;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class MongoRepositoryTests {
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
            Assert.Equal(1, organizations.Count);

            var organizations2 = _repository.GetByRetentionDaysEnabled(new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Count);

            Assert.NotEqual(organizations.First(), organizations2.First());
           
            organizations = _repository.GetByRetentionDaysEnabled(new PagingOptions());
            Assert.NotNull(organizations);
            Assert.Equal(2, organizations.Count);

            _repository.Remove(organizations);
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
    }
}