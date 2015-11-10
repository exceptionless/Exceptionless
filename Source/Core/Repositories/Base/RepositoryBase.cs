using System;
using System.Collections.Generic;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Repositories.Models;
using DataDictionary = Foundatio.Utility.DataDictionary;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new() {
        protected readonly IElasticIndex _index;

        public RepositoryBase(ElasticRepositoryContext<T> context, IElasticIndex index) : base(context) {
            _index = index;
        }

        protected override string GetTypeName() => EntityType.ToLower();

        protected override object CreateChangeTypeMessage(ChangeType changeType, T document, IDictionary<string, object> data = null) {
            return new ExtendedEntityChanged {
                ChangeType = changeType,
                Id = document?.Id,
                OrganizationId = (document as IOwnedByOrganization)?.OrganizationId,
                ProjectId = (document as IOwnedByProject)?.ProjectId,
                Type = EntityType,
                Data = new DataDictionary(data ?? new Dictionary<string, object>())
            };
        }
    }
}
