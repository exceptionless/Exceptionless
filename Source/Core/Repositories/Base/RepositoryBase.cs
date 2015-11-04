using System;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public class RepositoryBase<T> : Repository<T> where T : class, IIdentity, new() {
        protected readonly IElasticsearchIndex _index;

        public RepositoryBase(RepositoryContext<T> context, IElasticsearchIndex index) : base(context) {
            _index = index;
        }

        protected override string GetTypeName() => EntityType.ToLower();
    }
}
