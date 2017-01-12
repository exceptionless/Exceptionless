using System;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Elasticsearch.Queries.Options;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public class CustomSoftDeletesQueryBuilder : IElasticQueryBuilder {
        private const string Deleted = "deleted";

        public void Build<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var softDeletesQuery = ctx.GetSourceAs<ISoftDeletesQuery>();
            if (softDeletesQuery == null || softDeletesQuery.IncludeSoftDeletes)
                return;

            var idsQuery = ctx.GetSourceAs<IIdentityQuery>();
            var opt = ctx.GetOptionsAs<IElasticQueryOptions>();
            if (opt == null || !opt.SupportsSoftDeletes || (idsQuery != null && idsQuery.Ids.Count > 0))
                return;

            var missingFilter = new MissingFilter { Field = Deleted };
            var termFilter = new TermFilter { Field = Deleted, Value = softDeletesQuery.IncludeSoftDeletes };
            ctx.Filter &= (new FilterContainer(missingFilter) || new FilterContainer(termFilter));
        }
    }
}