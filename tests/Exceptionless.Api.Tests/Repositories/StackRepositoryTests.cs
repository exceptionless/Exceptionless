using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Utility;
using Nest;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class StackRepositoryTests : ElasticTestBase {
        private readonly InMemoryCacheClient _cache;
        private readonly IStackRepository _repository;

        public StackRepositoryTests(ITestOutputHelper output) : base(output) {
            _cache = _configuration.Cache as InMemoryCacheClient;
            _repository = GetService<IStackRepository>();
        }

        [Fact]
        public async Task CanGetByStackHashAsync() {
            Assert.Equal(0, _cache.Count);
            Assert.Equal(0, _cache.Hits);
            Assert.Equal(0, _cache.Misses);

            var stack = await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: SystemClock.UtcNow.SubtractMonths(1)), o => o.Cache());
            Assert.NotNull(stack?.Id);
            Assert.Equal(2, _cache.Count);
            Assert.Equal(0, _cache.Hits);
            Assert.Equal(0, _cache.Misses);

            var result = await _repository.GetStackBySignatureHashAsync(stack.ProjectId, stack.SignatureHash);
            Assert.Equal(stack.ToJson(), result.ToJson());
            Assert.Equal(2, _cache.Count);
            Assert.Equal(1, _cache.Hits);
            Assert.Equal(0, _cache.Misses);
        }

        [Fact]
        public async Task CanGetByFixedAsync() {
            var stack = await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));

            await _configuration.Client.RefreshAsync(Indices.All);
            var results = await _repository.GetByFilterAsync(null, "fixed:true", null, null, DateTime.MinValue, DateTime.MaxValue);
            Assert.NotNull(results);
            Assert.Equal(0, results.Total);

            results = await _repository.GetByFilterAsync(null, "fixed:false", null, null, DateTime.MinValue, DateTime.MaxValue);
            Assert.NotNull(results);
            Assert.Equal(1, results.Total);
            Assert.False(results.Documents.Single().IsRegressed);
            Assert.Null(results.Documents.Single().DateFixed);

            stack.MarkFixed();
            await _repository.SaveAsync(stack);
            await _configuration.Client.RefreshAsync(Indices.All);

            results = await _repository.GetByFilterAsync(null, "fixed:true", null, null, DateTime.MinValue, DateTime.MaxValue);
            Assert.NotNull(results);
            Assert.Equal(1, results.Total);
            Assert.False(results.Documents.Single().IsRegressed);
            Assert.NotNull(results.Documents.Single().DateFixed);

            results = await _repository.GetByFilterAsync(null, "fixed:false", null, null, DateTime.MinValue, DateTime.MaxValue);
            Assert.NotNull(results);
            Assert.Equal(0, results.Total);
        }

        [Fact]
        public async Task CanMarkAsRegressedAsync() {
            await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: SystemClock.UtcNow.SubtractMonths(1)));

            var stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.False(stack.IsRegressed);
            Assert.NotNull(stack.DateFixed);

            await _configuration.Client.RefreshAsync(Indices.All);
            await _repository.MarkAsRegressedAsync(TestConstants.StackId);

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.True(stack.IsRegressed);
            Assert.NotNull(stack.DateFixed);
        }

        [Fact]
        public async Task CanIncrementEventCounterAsync() {
            await _repository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId));

            var stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.NotNull(stack);
            Assert.Equal(0, stack.TotalOccurrences);
            Assert.Equal(DateTime.MinValue, stack.FirstOccurrence);
            Assert.Equal(DateTime.MinValue, stack.LastOccurrence);

            var utcNow = SystemClock.UtcNow;
            await _configuration.Client.RefreshAsync(Indices.All);
            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow, utcNow, 1);

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(1, stack.TotalOccurrences);
            Assert.Equal(utcNow, stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            await _configuration.Client.RefreshAsync(Indices.All);
            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow.SubtractDays(1), utcNow.SubtractDays(1), 1);

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(2, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow, stack.LastOccurrence);

            await _configuration.Client.RefreshAsync(Indices.All);
            await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId, utcNow.AddDays(1), utcNow.AddDays(1), 1);

            stack = await _repository.GetByIdAsync(TestConstants.StackId);
            Assert.Equal(3, stack.TotalOccurrences);
            Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
            Assert.Equal(utcNow.AddDays(1), stack.LastOccurrence);
        }

        [Fact]
        public async Task CanFindManyAsync() {
            Assert.Equal(0, await _repository.CountAsync());
            await _repository.AddAsync(StackData.GenerateSampleStacks());

            await _configuration.Client.RefreshAsync(Indices.All);
            var stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(1).PageLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Total);
            Assert.Equal(1, stacks.Documents.Count);

            var stacks2 = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(2).PageLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(1, stacks.Documents.Count);

            Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

            stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId);
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Documents.Count);

            await _repository.RemoveAsync(stacks.Documents);

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(0, await _repository.CountAsync());
        }
    }
}