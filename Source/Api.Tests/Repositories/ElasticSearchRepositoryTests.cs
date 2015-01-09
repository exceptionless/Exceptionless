using System;
using System.Linq;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ElasticSearchRepositoryTests {
        public readonly IEventRepository _repository = IoC.GetInstance<IEventRepository>();
        public readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();

        [Fact]
        public void CanCreateUpdateRemove() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            var ev = EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now);
            Assert.Null(ev.Id);

            _repository.Add(ev);
            Assert.NotNull(ev.Id);
            _client.Refresh();

            ev = _repository.GetById(ev.Id);
            Assert.NotNull(ev);

            ev.Message = "New Message";
            _repository.Save(ev);

            _repository.Remove(ev.Id);
        }

        [Fact]
        public void CanFindMany() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            _repository.Add(new[] {
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, occurrenceDate: DateTime.Now),
                EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, occurrenceDate: DateTime.Now)
            });

            _client.Refresh();

            var events = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(events);
            Assert.Equal(1, events.Count);

            var events2 = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithPage(2).WithLimit(1));
            Assert.NotNull(events);
            Assert.Equal(1, events.Count);

            Assert.NotEqual(events.First().Id, events2.First().Id);

            events = _repository.GetByStackId(TestConstants.StackId2);
            Assert.NotNull(events);
            Assert.Equal(2, events.Count);

            _repository.Remove(events);
            Assert.Equal(1, _repository.Count());
            _repository.RemoveAll();
        }

        [Fact]
        public void CanAddAndGetByCached() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            cache.FlushAll();

            var stack = StackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
            Assert.Null(stack.Id);

            Assert.Equal(0, cache.Count);
            _stackRepository.Add(stack, true);
            Assert.NotNull(stack.Id);
            Assert.True(cache.Count > 0);
            _client.Refresh();

            cache.FlushAll();
            Assert.Equal(0, cache.Count);
            Assert.NotNull(_stackRepository.GetById(stack.Id, true));
            Assert.True(cache.Count > 0);

            _stackRepository.RemoveAll();
            Assert.Equal(0, cache.Count);
        }
    }
}