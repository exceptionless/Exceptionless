using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ElasticSearchConfigurationTests {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        public readonly ElasticSearchConfiguration _configuration = IoC.GetInstance<ElasticSearchConfiguration>();
        public readonly EventIndex _eventIndex = new EventIndex();
        public readonly StackIndex _stackIndex = new StackIndex();
        public readonly EventRepository _eventRepository = IoC.GetInstance<EventRepository>();

        [Fact]
        public void CanCreateStackAlias() {
            _configuration.DeleteIndexes(_client);
            _configuration.ConfigureIndexes(_client);
            var index = _client.GetIndex(descriptor => descriptor.Index(_stackIndex.VersionedName));
            Assert.True(index.IsValid);
            Assert.Equal(1, index.Indices.Count);

            var alias = _client.GetAlias(descriptor => descriptor.Alias(_stackIndex.Name));
            Assert.True(alias.IsValid);
            Assert.Equal(1, alias.Indices.Count);
        }

        [Fact]
        public async Task CanCreateEventAlias() {
            _configuration.DeleteIndexes(_client);
            _configuration.ConfigureIndexes(_client);
            var indexes = _client.GetIndicesPointingToAlias(_eventIndex.Name);
            Assert.Equal(0, indexes.Count);

            var alias = _client.GetAlias(descriptor => descriptor.Alias(_eventIndex.Name));
            Assert.False(alias.IsValid);
            Assert.Equal(0, alias.Indices.Count);

            await _eventRepository.AddAsync(new PersistentEvent { Message = "Test", Type = Event.KnownTypes.Log, Date = DateTimeOffset.Now, OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, StackId = TestConstants.StackId });
            _client.Refresh();

            alias = _client.GetAlias(descriptor => descriptor.Alias(_eventIndex.Name));
            Assert.True(alias.IsValid);
            Assert.Equal(1, alias.Indices.Count);

            indexes = _client.GetIndicesPointingToAlias(_eventIndex.Name);
            Assert.Equal(1, indexes.Count);

            await _eventRepository.AddAsync(new PersistentEvent { Message = "Test", Type = Event.KnownTypes.Log, Date = DateTimeOffset.Now.SubtractMonths(1), OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, StackId = TestConstants.StackId });
            _client.Refresh();

            indexes = _client.GetIndicesPointingToAlias(_eventIndex.Name);
            Assert.Equal(2, indexes.Count);
        }
    }
}
