using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Repositories;

public sealed class StackRepositoryTests : IntegrationTestsBase
{
    private readonly InMemoryCacheClient _cache;
    private readonly StackData _stackData;
    private readonly IStackRepository _repository;

    public StackRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _cache = GetService<ICacheClient>() as InMemoryCacheClient ?? throw new InvalidOperationException();
        _stackData = GetService<StackData>();
        _repository = GetService<IStackRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task CanGetSoftDeletedStack()
    {
        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;

        await _repository.AddAsync(stack, o => o.ImmediateConsistency());

        var actual = _repository.GetByIdAsync(stack.Id, o => o.Cache("test"));
        Assert.NotNull(actual);
    }

    [Fact]
    public async Task CanGetNonExistentStack()
    {
        var stack = await _repository.GetByIdAsync(TestConstants.StackId, o => o.Cache("test"));
        Assert.Null(stack);
    }

    [Fact]
    public async Task CanGetByStatus()
    {
        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(organization);

        await _stackData.CreateSearchDataAsync(true);

        var appFilter = new AppFilter(organization);
        var stackIds = await _repository.GetIdsByQueryAsync(q => q.AppFilter(appFilter).FilterExpression("status:open OR status:regressed").DateRange(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow), o => o.PageLimit(o.GetMaxLimit()));
        Assert.Equal(2, stackIds.Total);
    }

    [Fact]
    public async Task CanGetByStackHashAsync()
    {
        long count = _cache.Count;
        long hits = _cache.Hits;
        long misses = _cache.Misses;

        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.UtcNow.SubtractMonths(1)), o => o.Cache());
        Assert.NotNull(stack?.Id);
        Assert.Equal(count + 2, _cache.Count);
        Assert.Equal(hits, _cache.Hits);
        Assert.Equal(misses, _cache.Misses);

        var result = await _repository.GetStackBySignatureHashAsync(stack.ProjectId, stack.SignatureHash);
        Assert.Equal(stack.ToJson(), result.ToJson());
        Assert.Equal(count + 2, _cache.Count);
        Assert.Equal(hits + 1, _cache.Hits);
        Assert.Equal(misses, _cache.Misses);
    }

    [Fact]
    public async Task CanGetByFixedAsync()
    {
        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId), o => o.ImmediateConsistency());

        var results = await _repository.FindAsync(q => q.FilterExpression("fixed:true"));
        Assert.NotNull(results);
        Assert.Equal(0, results.Total);

        results = await _repository.FindAsync(q => q.FilterExpression("fixed:false"));
        Assert.NotNull(results);
        Assert.Equal(1, results.Total);
        Assert.False(results.Documents.Single().Status == StackStatus.Regressed);
        Assert.Null(results.Documents.Single().DateFixed);

        stack.MarkFixed(null, TimeProvider);
        await _repository.SaveAsync(stack, o => o.ImmediateConsistency());

        results = await _repository.FindAsync(q => q.FilterExpression("fixed:true"));
        Assert.NotNull(results);
        Assert.Equal(1, results.Total);
        Assert.False(results.Documents.Single().Status == StackStatus.Regressed);
        Assert.NotNull(results.Documents.Single().DateFixed);

        results = await _repository.FindAsync(q => q.FilterExpression("fixed:false"));
        Assert.NotNull(results);
        Assert.Equal(0, results.Total);
    }

    [Fact]
    public async Task CanMarkAsRegressedAsync()
    {
        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.UtcNow.SubtractMonths(1)), o => o.ImmediateConsistency());
        Assert.NotNull(stack);
        Assert.False(stack.Status == StackStatus.Regressed);
        Assert.NotNull(stack.DateFixed);

        await _repository.MarkAsRegressedAsync(stack.Id);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(stack);
        Assert.True(stack.Status == StackStatus.Regressed);
        Assert.NotNull(stack.DateFixed);
    }

    [Fact]
    public async Task CanIncrementEventCounterAsync()
    {
        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId), o => o.ImmediateConsistency());
        Assert.NotNull(stack);
        Assert.Equal(0, stack.TotalOccurrences);
        Assert.True(stack.FirstOccurrence <= DateTime.UtcNow);
        Assert.True(stack.LastOccurrence <= DateTime.UtcNow);
        Assert.NotEqual(DateTime.MinValue, stack.CreatedUtc);
        Assert.NotEqual(DateTime.MinValue, stack.UpdatedUtc);
        Assert.Equal(stack.CreatedUtc, stack.UpdatedUtc);
        var updatedUtc = stack.UpdatedUtc;

        var utcNow = DateTime.UtcNow;
        await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow, utcNow, 1);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.Equal(1, stack.TotalOccurrences);
        Assert.Equal(utcNow, stack.FirstOccurrence);
        Assert.Equal(utcNow, stack.LastOccurrence);
        Assert.Equal(updatedUtc, stack.CreatedUtc);
        Assert.True(updatedUtc.IsBefore(stack.UpdatedUtc), $"Previous {updatedUtc}, Current: {stack.UpdatedUtc}");

        await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow.SubtractDays(1), utcNow.SubtractDays(1), 1);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.Equal(2, stack.TotalOccurrences);
        Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
        Assert.Equal(utcNow, stack.LastOccurrence);

        await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow.AddDays(1), utcNow.AddDays(1), 1);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.Equal(3, stack.TotalOccurrences);
        Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
        Assert.Equal(utcNow.AddDays(1), stack.LastOccurrence);
    }

    [Fact]
    public async Task CanFindManyAsync()
    {
        await _repository.AddAsync(_stackData.GenerateSampleStacks(), o => o.ImmediateConsistency());

        var stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(1).PageLimit(1));
        Assert.NotNull(stacks);
        Assert.Equal(3, stacks.Total);
        Assert.Single(stacks.Documents);

        var stacks2 = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(2).PageLimit(1));
        Assert.NotNull(stacks);
        Assert.Single(stacks.Documents);

        Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

        stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(stacks);
        Assert.Equal(3, stacks.Documents.Count);

        await _repository.RemoveAsync(stacks.Documents, o => o.ImmediateConsistency());
        Assert.Equal(0, await _repository.CountAsync());
    }

    [Fact]
    public async Task GetStacksForCleanupAsync()
    {
        var utcNow = DateTime.UtcNow;
        var openStack10DaysOldWithReference = _stackData.GenerateStack(id: TestConstants.StackId3, utcLastOccurrence: utcNow.SubtractDays(10), status: StackStatus.Open);
        openStack10DaysOldWithReference.References.Add("test");

        await _repository.AddAsync(new List<Stack> {
                _stackData.GenerateStack(id: TestConstants.StackId, utcLastOccurrence: utcNow.SubtractDays(5), status: StackStatus.Open),
                _stackData.GenerateStack(id: TestConstants.StackId2, utcLastOccurrence: utcNow.SubtractDays(10), status: StackStatus.Open),
                openStack10DaysOldWithReference,
                _stackData.GenerateStack(id: TestConstants.StackId4, utcLastOccurrence: utcNow.SubtractDays(10), status: StackStatus.Fixed)
            }, o => o.ImmediateConsistency());

        var stacks = await _repository.GetStacksForCleanupAsync(TestConstants.OrganizationId, utcNow.SubtractDays(8));
        Assert.NotNull(stacks);
        Assert.Equal(1, stacks.Total);
        Assert.Single(stacks.Documents);
        Assert.Equal(TestConstants.StackId2, stacks.Documents.Single().Id);

        stacks = await _repository.GetStacksForCleanupAsync(TestConstants.OrganizationId, utcNow.SubtractDays(1));
        Assert.NotNull(stacks);
        Assert.Equal(2, stacks.Total);
        Assert.Equal(2, stacks.Documents.Count);
        Assert.NotNull(stacks.Documents.SingleOrDefault(s => String.Equals(s.Id, TestConstants.StackId)));
        Assert.NotNull(stacks.Documents.SingleOrDefault(s => String.Equals(s.Id, TestConstants.StackId2)));
    }
}
