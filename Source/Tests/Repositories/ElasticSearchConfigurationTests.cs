﻿using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ElasticSearchConfigurationTests {
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly ElasticConfiguration _configuration = IoC.GetInstance<ElasticConfiguration>();
        private readonly EventIndex _eventIndex = new EventIndex();
        private readonly StackIndex _stackIndex = new StackIndex();
        private readonly EventRepository _eventRepository = IoC.GetInstance<EventRepository>();

        [Fact]
        public async Task CanCreateStackAliasAsync() {
            _configuration.DeleteIndexes(_client);
            _configuration.ConfigureIndexes(_client);
            var index = await _client.GetIndexAsync(Indices.Index(_stackIndex.VersionedName));
            Assert.True(index.IsValid);
            Assert.Equal(1, index.Indices.Count);

            var alias = _client.GetAlias(descriptor => descriptor.Alias(_stackIndex.AliasName));
            Assert.True(alias.IsValid);
            Assert.Equal(1, alias.Indices.Count);
        }

        [Fact]
        public async Task CanCreateEventAliasAsync() {
            _configuration.DeleteIndexes(_client);
            _configuration.ConfigureIndexes(_client);
            await _client.RefreshAsync(Indices.All);

            var indexes = await _client.GetIndicesPointingToAliasAsync(_eventIndex.AliasName);
            Assert.Equal(0, indexes.Count);

            var alias = await _client.GetAliasAsync(descriptor => descriptor.Alias(_eventIndex.AliasName));
            Assert.False(alias.IsValid);
            Assert.Equal(0, alias.Indices.Count);

            var ev = await _eventRepository.AddAsync(new PersistentEvent { Message = "Test", Type = Event.KnownTypes.Log, Date = DateTimeOffset.Now.StartOfMonth().AddDays(1), OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, StackId = TestConstants.StackId });
            Assert.NotNull(ev?.Id);
            Assert.True(ObjectId.Parse(ev.Id).CreationTime.IntersectsMinute(DateTime.UtcNow));

            await _client.RefreshAsync(Indices.All);
            alias = await _client.GetAliasAsync(descriptor => descriptor.Alias(_eventIndex.AliasName));
            Assert.True(alias.IsValid);
            Assert.Equal(1, alias.Indices.Count);

            indexes = await _client.GetIndicesPointingToAliasAsync(_eventIndex.AliasName);
            Assert.Equal(1, indexes.Count);

            var date = DateTimeOffset.UtcNow.StartOfMonth().SubtractSeconds(1).ToLocalTime();
            ev = await _eventRepository.AddAsync(new PersistentEvent { Message = "Test", Type = Event.KnownTypes.Log, Date = date, OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, StackId = TestConstants.StackId });
            Assert.NotNull(ev?.Id);
            Assert.Equal(date, ObjectId.Parse(ev.Id).CreationTime);

            await _client.RefreshAsync(Indices.All);
            indexes = await _client.GetIndicesPointingToAliasAsync(_eventIndex.AliasName);
            Assert.Equal(2, indexes.Count);
        }
    }
}
