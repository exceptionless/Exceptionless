using System;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new() {
        protected readonly IElasticIndex _index;

        public RepositoryBase(ElasticRepositoryContext<T> context, IElasticIndex index) : base(context) {
            _index = index;
        }

        protected override string GetTypeName() => EntityType.ToLower();
    }
}
