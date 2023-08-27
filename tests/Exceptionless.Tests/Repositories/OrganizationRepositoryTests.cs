using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Xunit;
using Xunit.Abstractions;
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

        await _repository.AddAsync(organization);
        await RefreshDataAsync();
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
        await _repository.AddAsync(organization, o => o.Cache());
        await RefreshDataAsync();
        Assert.NotNull(organization.Id);
        Assert.Equal(1, _cache.Count);

        await _cache.RemoveAllAsync();
        Assert.Equal(0, _cache.Count);
        await _repository.GetByIdAsync(organization.Id, o => o.Cache());
        Assert.NotNull(organization.Id);
        Assert.Equal(1, _cache.Count);

        await _repository.RemoveAllAsync();
        await RefreshDataAsync();
        Assert.Equal(0, _cache.Count);
    }
}
