using System;
using System.Linq;
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

        public ElasticSearchRepositoryTests() {
            _eventRepository.RemoveAll();
            _repository.RemoveAll();
        }

        [Fact]
        public void CanCreateUpdateRemove() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            var stack = StackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
            Assert.Null(stack.Id);

            _repository.Add(stack);
            Assert.NotNull(stack.Id);
            _client.Refresh();

            stack = _repository.GetById(stack.Id);
            Assert.NotNull(stack);

            stack.Description = "New Description";
            _repository.Save(stack);

            _repository.Remove(stack.Id);
        }

        [Fact]
        public void CanFindMany() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            _repository.Add(StackData.GenerateSampleStacks());

            _client.Refresh();

            var stacks = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Total);
            Assert.Equal(1, stacks.Documents.Count);

            var stacks2 = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(stacks);
            Assert.Equal(1, stacks.Documents.Count);

            Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

            stacks = _repository.GetByOrganizationId(TestConstants.OrganizationId);
            Assert.NotNull(stacks);
            Assert.Equal(3, stacks.Documents.Count);

            _repository.Remove(stacks.Documents);
            Assert.Equal(0, _repository.Count());
            _repository.RemoveAll();
        }

        [Fact]
        public void CanAddAndGetByCached() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            await cache.RemoveAllAsync().AnyContext();

            var stack = StackData.GenerateSampleStack();

            Assert.Equal(0, cache.Count);
            _repository.Add(stack, true);
            Assert.NotNull(stack.Id);
            Assert.Equal(2, cache.Count);
            _client.Refresh();

            await cache.RemoveAllAsync().AnyContext();
            Assert.Equal(0, cache.Count);
            Assert.NotNull(_repository.GetById(stack.Id, true));
            Assert.Equal(1, cache.Count);

            _repository.RemoveAll();
            Assert.Equal(0, cache.Count);
        }
    }
}