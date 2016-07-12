using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Logging;
using Foundatio.Repositories.Models;
using DataDictionary = Foundatio.Utility.DataDictionary;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new() {
        protected readonly IElasticIndex _index;

        public RepositoryBase(ElasticRepositoryContext<T> context, IElasticIndex index, ILoggerFactory loggerFactory = null) : base(context, loggerFactory) {
            _index = index;
        }

        protected override string GetTypeName() => EntityType.ToLower();

        protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, T document, IDictionary<string, object> data = null, TimeSpan? delay = null) {
          return PublishMessageAsync(new ExtendedEntityChanged {
                ChangeType = changeType,
                Id = document?.Id,
                OrganizationId = (document as IOwnedByOrganization)?.OrganizationId,
                ProjectId = (document as IOwnedByProject)?.ProjectId,
                Type = EntityType,
                Data = new DataDictionary(data ?? new Dictionary<string, object>())
            }, delay);
        }
    }
}
