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
        private readonly ElasticClient _client = IoC.GetInstance<ElasticClient>();

        [Fact]
        public void CanCreateUpdateRemove() {
            _repository.RemoveAll();
            Assert.Equal(0, _repository.Count());

            var ev = EventData.GenerateEvent(generateId: false, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now, nestingLevel: 5, minimiumNestingLevel: 1);
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
                EventData.GenerateEvent(id: TestConstants.EventId2, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now, nestingLevel: 5, minimiumNestingLevel: 1),
                EventData.GenerateEvent(id: TestConstants.EventId3, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, occurrenceDate: DateTime.Now, nestingLevel: 5, minimiumNestingLevel: 1),
                EventData.GenerateEvent(id: TestConstants.EventId4, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, occurrenceDate: DateTime.Now, nestingLevel: 5, minimiumNestingLevel: 1)
            });

            _client.Refresh();

            var events = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(events);
            Assert.Equal(1, events.Count);

            var events2 = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithPage(1).WithLimit(1));
            Assert.NotNull(events);
            Assert.Equal(1, events.Count);

            Assert.NotEqual(events.First(), events2.First());

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

            var ev = EventData.GenerateEvent(generateId: false, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now, nestingLevel: 5, minimiumNestingLevel: 1);
            Assert.Null(ev.Id);

            Assert.Equal(0, cache.Count);
            _repository.Add(ev, true);
            Assert.NotNull(ev.Id);
            Assert.Equal(1, cache.Count);
            _client.Refresh();

            cache.FlushAll();
            Assert.Equal(0, cache.Count);
            Assert.NotNull(_repository.GetById(ev.Id, true));
            Assert.Equal(1, cache.Count);

            _repository.RemoveAll();
            Assert.Equal(0, cache.Count);
        }
    }
}