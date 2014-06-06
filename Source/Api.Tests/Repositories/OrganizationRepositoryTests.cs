using System;
using System.Collections.Generic;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class OrganizationRepositoryTests {
        public readonly IOrganizationRepository _repository = IoC.GetInstance<IOrganizationRepository>();

        [Fact]
        public void CanIncrementUsage() {
            var cache = IoC.GetInstance<ICacheClient>() as InMemoryCacheClient;
            Assert.NotNull(cache);
            cache.FlushAll();

            var messages = new List<PlanOverage>();
            var messagePublisher = IoC.GetInstance<IMessagePublisher>() as InMemoryMessageBus;
            Assert.NotNull(messagePublisher);
            messagePublisher.Subscribe<PlanOverage>(messages.Add);

            var organization = _repository.Add(new Organization { Name = "Test", MaxEventsPerMonth = 750 });
            Assert.False(_repository.IncrementUsage(organization.Id));
            Assert.Equal(0, messages.Count);

            Assert.True(_repository.IncrementUsage(organization.Id, 6));
            Assert.Equal(1, messages.Count);
            
            // TODO: Verify the what counts when you have 1 left and you submit 2.
        }
    }
}