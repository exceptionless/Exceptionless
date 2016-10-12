using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IOrganizationIdQuery {
        List<string> OrganizationIds { get; }
    }

    public class OrganizationIdQueryBuilder : IElasticQueryBuilder {
        public void Build<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var organizationIdQuery = ctx.GetSourceAs<IOrganizationIdQuery>();
            if (organizationIdQuery?.OrganizationIds == null || organizationIdQuery.OrganizationIds.Count <= 0)
                return;

            if (organizationIdQuery.OrganizationIds.Count == 1)
                ctx.Query &= Query<T>.Term("organization", organizationIdQuery.OrganizationIds.First());
            else
                ctx.Query &= Query<T>.Terms(t => t.Field("organization").Terms(organizationIdQuery.OrganizationIds));
        }
    }

    public static class OrganizationIdQueryExtensions {
        public static T WithOrganizationId<T>(this T query, string id) where T : IOrganizationIdQuery {
            if (!String.IsNullOrEmpty(id))
                query.OrganizationIds.Add(id);
            return query;
        }

        public static T WithOrganizationIds<T>(this T query, params string[] ids) where T : IOrganizationIdQuery {
            query.OrganizationIds.AddRange(ids.Distinct());
            return query;
        }

        public static T WithOrganizationIds<T>(this T query, IEnumerable<string> ids) where T : IOrganizationIdQuery {
            query.OrganizationIds.AddRange(ids.Distinct());
            return query;
        }
    }
}
