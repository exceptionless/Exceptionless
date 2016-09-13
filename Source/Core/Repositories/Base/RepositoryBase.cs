using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using DataDictionary = Foundatio.Utility.DataDictionary;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new() {

        public RepositoryBase(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {}
        
        protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, T document, IDictionary<string, object> data = null, TimeSpan? delay = null) {
          return PublishMessageAsync(new ExtendedEntityChanged {
                ChangeType = changeType,
                Id = document?.Id,
                OrganizationId = (document as IOwnedByOrganization)?.OrganizationId,
                ProjectId = (document as IOwnedByProject)?.ProjectId,
                StackId = (document as IOwnedByStack)?.StackId,
                Type = EntityTypeName,
                Data = new DataDictionary(data ?? new Dictionary<string, object>())
            }, delay);
        }
    }
}
