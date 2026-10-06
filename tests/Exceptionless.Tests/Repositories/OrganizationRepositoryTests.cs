using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Tests.Utility;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetByCriteriaAsync_SearchById_ReturnsMatchingOrganization(bool padCriteria)
    {
        // Arrange
        var organization = new Organization { Name = "Criteria Test Organization", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync(organization, o => o.ImmediateConsistency());

        // Act
        string criteria = padCriteria ? $"  {organization.Id}  " : organization.Id;
        var results = await _repository.GetByCriteriaAsync(criteria,
            o => o.PageLimit(10), OrganizationSortBy.Newest);

        // Assert
        Assert.Single(results.Documents);
        Assert.Equal(organization.Id, results.Documents.First().Id);
    }

    [Theory]
    [InlineData("NSW Planning Team")]
    [InlineData("NSW")]
    [InlineData("nsw")]
    [InlineData("planning")]
    [InlineData("NSW planning")]
    [InlineData("  NSW  ")]
    public async Task GetByCriteriaAsync_SearchByName_ReturnsMatchingOrganization(string criteria)
    {
        // Arrange
        var organization = new Organization { Name = "NSW Planning Team", PlanId = _plans.FreePlan.Id };
        var unrelatedOrganization = new Organization { Name = "Unrelated Company", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync([organization, unrelatedOrganization], o => o.ImmediateConsistency());

        // Act
        var results = await _repository.GetByCriteriaAsync(criteria,
            o => o.PageLimit(10), OrganizationSortBy.Newest);

        // Assert
        Assert.Single(results.Documents);
        Assert.Equal(organization.Id, results.Documents.First().Id);
    }

    [Fact]
    public async Task GetByCriteriaAsync_WithSymbolOnlyName_ReturnsMatchingOrganization()
    {
        var organization = new Organization { Name = "+++", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync(organization, options => options.ImmediateConsistency());

        var results = await _repository.GetByCriteriaAsync(organization.Name,
            options => options.PageLimit(10), OrganizationSortBy.Newest);

        Assert.Equal(organization.Id, Assert.Single(results.Documents).Id);
    }

    [Fact]
    public async Task GetByCriteriaAsync_WithNameAndPlanAndStatusFilters_ReturnsMatchingOrganization()
    {
        var organization = new Organization
        {
            Name = "NSW Planning Team",
            PlanId = _plans.SmallPlan.Id,
            BillingStatus = BillingStatus.Active
        };
        await _repository.AddAsync([
            organization,
            new Organization { Name = "NSW Free", PlanId = _plans.FreePlan.Id, BillingStatus = BillingStatus.Active },
            new Organization { Name = "NSW Past Due", PlanId = _plans.SmallPlan.Id, BillingStatus = BillingStatus.PastDue },
            new Organization { Name = "Unrelated Company", PlanId = _plans.SmallPlan.Id, BillingStatus = BillingStatus.Active }
        ], options => options.ImmediateConsistency());

        var results = await _repository.GetByCriteriaAsync("nsw", options => options.PageLimit(10),
            OrganizationSortBy.Newest, paid: true, suspended: false);

        Assert.Equal(organization.Id, Assert.Single(results.Documents).Id);
    }

    [Theory]
    [InlineData(null, new[] { "Beta Organization", "Zulu Organization", "alpha Organization" })]
    [InlineData("-name", new[] { "alpha Organization", "Zulu Organization", "Beta Organization" })]
    public async Task GetByFilterAsync_WithSearchAfterPaging_ReturnsOnlyAllowedOrganizationsInRequestedOrder(string? sort, string[] expectedNames)
    {
        // Arrange
        var zulu = new Organization { Name = "Zulu Organization", PlanId = _plans.FreePlan.Id };
        var alpha = new Organization { Name = "alpha Organization", PlanId = _plans.FreePlan.Id };
        var beta = new Organization { Name = "Beta Organization", PlanId = _plans.FreePlan.Id };
        var blocked = new Organization { Name = "Aardvark Organization", PlanId = _plans.FreePlan.Id };
        await _repository.AddAsync([zulu, alpha, beta, blocked], o => o.ImmediateConsistency());
        var filter = new AppFilter(new[] { zulu, alpha, beta }) { IsUserOrganizationsFilter = true };

        // Act
        var results = await _repository.GetByFilterAsync(filter, null, sort, o => o.SearchAfterPaging().PageLimit(2));
        var firstPage = results.Documents.ToList();
        bool hasSecondPage = await results.NextPageAsync();
        var secondPage = results.Documents.ToList();
        bool hasThirdPage = await results.NextPageAsync();

        // Assert
        Assert.Equal(2, firstPage.Count);
        Assert.True(hasSecondPage);
        Assert.Single(secondPage);
        Assert.False(hasThirdPage);
        var organizations = firstPage.Concat(secondPage).ToList();
        Assert.Equal(expectedNames, organizations.Select(organization => organization.Name));
        Assert.Equal(3, organizations.Select(organization => organization.Id).Distinct().Count());
        Assert.DoesNotContain(organizations, organization => String.Equals(organization.Id, blocked.Id, StringComparison.Ordinal));
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
    public async Task GetByCriteria_SuspendedFilter_ReturnsExpectedOrganizations()
    {
        // Arrange
        var activeOrganization = new Organization { Name = "Active Organization", PlanId = _plans.FreePlan.Id, BillingStatus = BillingStatus.Active };
        var trialingOrganization = new Organization { Name = "Trialing Organization", PlanId = _plans.FreePlan.Id, BillingStatus = BillingStatus.Trialing };
        var canceledOrganization = new Organization { Name = "Canceled Organization", PlanId = _plans.FreePlan.Id, BillingStatus = BillingStatus.Canceled };
        var pastDueOrganization = new Organization { Name = "Past Due Organization", PlanId = _plans.FreePlan.Id, BillingStatus = BillingStatus.PastDue };
        var unpaidOrganization = new Organization { Name = "Unpaid Organization", PlanId = _plans.FreePlan.Id, BillingStatus = BillingStatus.Unpaid };
        var manuallySuspendedOrganization = new Organization
        {
            Name = "Manual Suspension Organization",
            PlanId = _plans.FreePlan.Id,
            BillingStatus = BillingStatus.Active,
            IsSuspended = true,
            SuspensionCode = SuspensionCode.Abuse,
            SuspensionDate = DateTime.UtcNow,
            SuspendedByUserId = TestConstants.UserId
        };
        await _repository.AddAsync([
            activeOrganization,
            trialingOrganization,
            canceledOrganization,
            pastDueOrganization,
            unpaidOrganization,
            manuallySuspendedOrganization
        ], o => o.ImmediateConsistency());

        // Act
        var suspendedResults = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Newest, suspended: true);
        var activeResults = await _repository.GetByCriteriaAsync(null,
            o => o.PageLimit(10), OrganizationSortBy.Newest, suspended: false);

        // Assert
        Assert.Contains(suspendedResults.Documents, o => String.Equals(o.Id, pastDueOrganization.Id, StringComparison.Ordinal));
        Assert.Contains(suspendedResults.Documents, o => String.Equals(o.Id, unpaidOrganization.Id, StringComparison.Ordinal));
        Assert.Contains(suspendedResults.Documents, o => String.Equals(o.Id, manuallySuspendedOrganization.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(suspendedResults.Documents, o => String.Equals(o.Id, activeOrganization.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(suspendedResults.Documents, o => String.Equals(o.Id, trialingOrganization.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(suspendedResults.Documents, o => String.Equals(o.Id, canceledOrganization.Id, StringComparison.Ordinal));

        Assert.Contains(activeResults.Documents, o => String.Equals(o.Id, activeOrganization.Id, StringComparison.Ordinal));
        Assert.Contains(activeResults.Documents, o => String.Equals(o.Id, trialingOrganization.Id, StringComparison.Ordinal));
        Assert.Contains(activeResults.Documents, o => String.Equals(o.Id, canceledOrganization.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(activeResults.Documents, o => String.Equals(o.Id, pastDueOrganization.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(activeResults.Documents, o => String.Equals(o.Id, unpaidOrganization.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(activeResults.Documents, o => String.Equals(o.Id, manuallySuspendedOrganization.Id, StringComparison.Ordinal));
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
