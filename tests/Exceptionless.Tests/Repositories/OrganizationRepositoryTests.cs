using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Repositories;

public sealed class OrganizationRepositoryTests : IntegrationTestsBase
{
    private readonly InMemoryCacheClient _cache;
    private readonly IOrganizationRepository _repository;
    private readonly BillingPlans _plans;

    public OrganizationRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        Log.SetLogLevel<OrganizationRepository>(LogLevel.Trace);
        _cache = GetService<ICacheClient>() as InMemoryCacheClient ?? throw new InvalidOperationException();
        _repository = GetService<IOrganizationRepository>();
        _plans = GetService<BillingPlans>();
    }

    [Fact]
    public async Task CanCreateUpdateRemoveAsync()
    {
        Assert.Equal(0, await _repository.CountAsync());

        var organization = new Organization { Name = "Test Organization", PlanId = _plans.FreePlan.Id };
        Assert.Null(organization.Id);

        await _repository.AddAsync(organization, o => o.ImmediateConsistency());
        Assert.NotNull(organization.Id);

        organization = await _repository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);

        organization.Name = "New organization";
        await _repository.SaveAsync(organization);
        await _repository.RemoveAsync(organization.Id);
    }

    [Fact]
    public async Task CanAddAndGetByCachedAsync()
    {
        var organization = new Organization { Name = "Test Organization", PlanId = _plans.FreePlan.Id };
        Assert.Null(organization.Id);

        Assert.Equal(0, _cache.Count);
        await _repository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        Assert.NotNull(organization.Id);
        Assert.Equal(1, _cache.Count);

        await _cache.RemoveAllAsync();
        Assert.Equal(0, _cache.Count);
        await _repository.GetByIdAsync(organization.Id, o => o.Cache());
        Assert.NotNull(organization.Id);
        Assert.Equal(1, _cache.Count);

        await _repository.RemoveAllAsync(o => o.ImmediateConsistency());
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task GetByCriteria_SearchById_ReturnsMatchingOrganization()
    {
        // Arrange
        var organization = new Organization { Name = "Criteria Test Organization", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync(organization, o => o.ImmediateConsistency());

        // Act
        var results = await _repository.GetByCriteriaAsync(organization.Id,
            o => o.PageLimit(10), OrganizationSortBy.Newest);

        // Assert
        Assert.Single(results.Documents);
        Assert.Equal(organization.Id, results.Documents.First().Id);
    }

    [Fact]
    public async Task GetByCriteria_SearchByName_ReturnsMatchingOrganization()
    {
        // Arrange
        var organization = new Organization { Name = "Unique Search Name", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync(organization, o => o.ImmediateConsistency());

        // Act
        var results = await _repository.GetByCriteriaAsync("Unique Search Name",
            o => o.PageLimit(10), OrganizationSortBy.Newest);

        // Assert
        Assert.Single(results.Documents);
        Assert.Equal("Unique Search Name", results.Documents.First().Name);
    }

    [Fact]
    public async Task GetByCriteria_PaidFilter_ExcludesFreeOrganizations()
    {
        // Arrange
        var freeOrganization = new Organization { Name = "Free Organization", PlanId = _plans.FreePlan.Id };
        var paidOrganization = new Organization { Name = "Paid Organization", PlanId = _plans.SmallPlan.Id };
        await _repository.AddAsync([freeOrganization, paidOrganization], o => o.ImmediateConsistency());

        // Act
        var paidResults = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Newest, paid: true);
        var freeResults = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Newest, paid: false);

        // Assert
        Assert.All(paidResults.Documents, d => Assert.NotEqual(_plans.FreePlan.Id, d.PlanId));
        Assert.All(freeResults.Documents, d => Assert.Equal(_plans.FreePlan.Id, d.PlanId));
    }

    [Fact]
    public async Task GetByCriteria_SortByName_ReturnsSortedResults()
    {
        // Arrange
        var organizationC = new Organization { Name = "Charlie Organization", PlanId = _plans.FreePlan.Id };
        var organizationA = new Organization { Name = "Alpha Organization", PlanId = _plans.FreePlan.Id };
        var organizationB = new Organization { Name = "Bravo Organization", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync([organizationC, organizationA, organizationB], o => o.ImmediateConsistency());

        // Act
        var results = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Alphabetical);

        // Assert
        var names = results.Documents.Select(d => d.Name).ToList();
        Assert.Equal(names.OrderBy(n => n), names);
    }
}
