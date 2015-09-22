using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
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

        public ElasticSearchRepositoryTests() {
            _eventRepository.RemoveAllAsync().AnyContext().GetAwaiter().GetResult();
            _repository.RemoveAllAsync().AnyContext().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanCreateUpdateRemove() {
            await _repository.RemoveAllAsync().AnyContext();
            Assert.Equal(0, await _repository.CountAsync().AnyContext());

            var stack = StackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
            Assert.Null(stack.Id);

            await _repository.AddAsync(stack).AnyContext();
            Assert.NotNull(stack.Id);
            _client.Refresh();

            stack = await _repository.GetByIdAsync(stack.Id).AnyContext();
            Assert.NotNull(stack);

            stack.Description = "New Description";
            await _repository.SaveAsync(stack).AnyContext();
            await _repository.RemoveAsync(stack.Id).AnyContext();
        }

        [Fact]
        public async Task CanFindMany() {
            await _repository.RemoveAllAsync().AnyContext();
            Assert.Equal(0, await _repository.CountAsync().AnyContext());

            await _repository.AddAsync(StackData.GenerateSampleStacks()).AnyContext();

            _client.Refresh();

            var stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(1).WithLimit(1)).AnyContext();
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Total);
            Assert.Equal(1, stacks.Documents.Count);

            var stacks2 = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(2).WithLimit(1)).AnyContext();
            Assert.NotNull(stacks);
            Assert.Equal(1, stacks.Documents.Count);

            Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

            stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId).AnyContext();
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Documents.Count);

            await _repository.RemoveAsync(stacks.Documents).AnyContext();
            Assert.Equal(0, await _repository.CountAsync().AnyContext());
            await _repository.RemoveAllAsync().AnyContext();
        }

        [Fact]
        public async Task CanAddAndGetByCached() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync().AnyContext();

            var stack = StackData.GenerateSampleStack();

            Assert.Equal(0, cache.Count);
            await _repository.AddAsync(stack, true).AnyContext();
            Assert.NotNull(stack.Id);
            Assert.Equal(2, cache.Count);
            _client.Refresh();

            await cache.RemoveAllAsync().AnyContext();
            Assert.Equal(0, cache.Count);
            Assert.NotNull(await _repository.GetByIdAsync(stack.Id, true).AnyContext());
            Assert.Equal(1, cache.Count);

            await _repository.RemoveAllAsync().AnyContext();
            Assert.Equal(0, cache.Count);
        }
    }
}