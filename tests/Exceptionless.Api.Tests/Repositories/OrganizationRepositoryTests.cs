using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Repositories;
using Nest;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel; 

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class OrganizationRepositoryTests : ElasticTestBase {
        private readonly InMemoryCacheClient _cache;
        private readonly IOrganizationRepository _repository;

        public OrganizationRepositoryTests(ITestOutputHelper output) : base(output) {
            _cache = GetService<ICacheClient>() as InMemoryCacheClient;
            _repository = GetService<IOrganizationRepository>();

            Log.SetLogLevel<OrganizationRepository>(LogLevel.Trace);
        }

        [Fact]
        public async Task CanCreateUpdateRemoveAsync() {
            Assert.Equal(0, await _repository.CountAsync());

            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            await _repository.AddAsync(organization);
            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.NotNull(organization.Id);

            organization = await _repository.GetByIdAsync(organization.Id);
            Assert.NotNull(organization);

            organization.Name = "New organization";
            await _repository.SaveAsync(organization);

            await _repository.RemoveAsync(organization.Id);
        }

        [Fact]
        public async Task CanFindManyAsync() {
            Assert.Equal(0, await _repository.CountAsync());

            await _repository.AddAsync(new[] {
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 0 },
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 1 },
                new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id, RetentionDays = 2 }
            });

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(3, await _repository.CountAsync());

            var organizations = await _repository.GetByRetentionDaysEnabledAsync(o => o.PageNumber(1).PageLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            var organizations2 = await _repository.GetByRetentionDaysEnabledAsync(o => o.PageNumber(2).PageLimit(1));
            Assert.NotNull(organizations);
            Assert.Equal(1, organizations.Documents.Count);

            Assert.NotEqual(organizations.Documents.First(), organizations2.Documents.First());

            organizations = await _repository.GetByRetentionDaysEnabledAsync();
            Assert.NotNull(organizations);
            Assert.Equal(2, organizations.Total);

            await _repository.RemoveAsync(organizations.Documents);
            await _configuration.Client.RefreshAsync(Indices.All);

            Assert.Equal(1, await _repository.CountAsync());
            await _repository.RemoveAllAsync();
            await _configuration.Client.RefreshAsync(Indices.All);
        }

        [Fact]
        public async Task CanAddAndGetByCachedAsync() {
            var organization = new Organization { Name = "Test Organization", PlanId = BillingManager.FreePlan.Id };
            Assert.Null(organization.Id);

            Assert.Equal(0, _cache.Count);
            await _repository.AddAsync(organization, o => o.Cache());
            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.NotNull(organization.Id);
            Assert.Equal(1, _cache.Count);

            await _cache.RemoveAllAsync();
            Assert.Equal(0, _cache.Count);
            await _repository.GetByIdAsync(organization.Id, o => o.Cache());
            Assert.NotNull(organization.Id);
            Assert.Equal(1, _cache.Count);

            await _repository.RemoveAllAsync();
            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(0, _cache.Count);
        }
    }
}