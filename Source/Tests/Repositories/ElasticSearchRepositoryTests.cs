using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ElasticSearchRepositoryTests {
        public readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        public readonly IStackRepository _repository = IoC.GetInstance<IStackRepository>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        
        [Fact]
        public async Task CanCreateUpdateRemoveAsync() {
            await ResetAsync();

            await _repository.RemoveAllAsync();
            Assert.Equal(0, await _repository.CountAsync());

            var stack = StackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
            Assert.Null(stack.Id);

            await _repository.AddAsync(stack);
            Assert.NotNull(stack.Id);
            await _client.RefreshAsync();

            stack = await _repository.GetByIdAsync(stack.Id);
            Assert.NotNull(stack);

            stack.Description = "New Description";
            await _repository.SaveAsync(stack);
            await _repository.RemoveAsync(stack.Id);
        }

        [Fact]
        public async Task CanFindManyAsync() {
            await ResetAsync();

            await _repository.RemoveAllAsync();
            Assert.Equal(0, await _repository.CountAsync());

            await _repository.AddAsync(StackData.GenerateSampleStacks());

            await _client.RefreshAsync();

            var stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Total);
            Assert.Equal(1, stacks.Documents.Count);

            var stacks2 = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(1, stacks.Documents.Count);

            Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

            stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId);
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Documents.Count);

            await _repository.RemoveAsync(stacks.Documents);
            Assert.Equal(0, await _repository.CountAsync());
            await _repository.RemoveAllAsync();
        }

        [Fact]
        public async Task CanAddAndGetByCachedAsync() {
            await ResetAsync();

            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync();

            var stack = StackData.GenerateSampleStack();

            Assert.Equal(0, cache.Count);
            await _repository.AddAsync(stack, true);
            Assert.NotNull(stack.Id);
            Assert.Equal(2, cache.Count);
            await _client.RefreshAsync();

            await cache.RemoveAllAsync();
            Assert.Equal(0, cache.Count);
            Assert.NotNull(await _repository.GetByIdAsync(stack.Id, true));
            Assert.Equal(1, cache.Count);

            await _repository.RemoveAllAsync();
            Assert.Equal(0, cache.Count);
        }
        
        private bool _isReset;
        private async Task ResetAsync() {
            if (!_isReset) {
                _isReset = true;
                await _eventRepository.RemoveAllAsync();
                await _repository.RemoveAllAsync();
            }
        }

    }
}