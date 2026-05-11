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
        var organization = new Organization { Name = "Criteria Test Org", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync(organization, o => o.ImmediateConsistency());

        var results = await _repository.GetByCriteriaAsync(organization.Id,
            o => o.PageLimit(10), OrganizationSortBy.Newest);

        Assert.Single(results.Documents);
        Assert.Equal(organization.Id, results.Documents.First().Id);
    }

    [Fact]
    public async Task GetByCriteria_SearchByName_ReturnsMatchingOrganization()
    {
        var organization = new Organization { Name = "Unique Search Name", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync(organization, o => o.ImmediateConsistency());

        var results = await _repository.GetByCriteriaAsync("Unique Search Name",
            o => o.PageLimit(10), OrganizationSortBy.Newest);

        Assert.Single(results.Documents);
        Assert.Equal("Unique Search Name", results.Documents.First().Name);
    }

    [Fact]
    public async Task GetByCriteria_PaidFilter_ExcludesFreeOrganizations()
    {
        var freeOrg = new Organization { Name = "Free Org", PlanId = _plans.FreePlan.Id };
        var paidOrg = new Organization { Name = "Paid Org", PlanId = _plans.SmallPlan.Id };
        await _repository.AddAsync([freeOrg, paidOrg], o => o.ImmediateConsistency());

        var paidResults = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Newest, paid: true);
        Assert.All(paidResults.Documents, d => Assert.NotEqual(_plans.FreePlan.Id, d.PlanId));

        var freeResults = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Newest, paid: false);
        Assert.All(freeResults.Documents, d => Assert.Equal(_plans.FreePlan.Id, d.PlanId));
    }

    [Fact]
    public async Task GetByCriteria_SortByName_ReturnsSortedResults()
    {
        var orgC = new Organization { Name = "Charlie Org", PlanId = _plans.FreePlan.Id };
        var orgA = new Organization { Name = "Alpha Org", PlanId = _plans.FreePlan.Id };
        var orgB = new Organization { Name = "Bravo Org", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync([orgC, orgA, orgB], o => o.ImmediateConsistency());

        var results = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Alphabetical);

        var names = results.Documents.Select(d => d.Name).ToList();
        Assert.Equal(names.OrderBy(n => n), names);
    }
}
