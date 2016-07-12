using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Elasticsearch.Repositories.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IOrganizationIdQuery {
        List<string> OrganizationIds { get; }
    }

    public class OrganizationIdQueryBuilder : QueryBuilderBase {
        public override void BuildFilter<T>(object query, object options, ref FilterContainer container) {
            var organizationIdQuery = query as IOrganizationIdQuery;
            if (organizationIdQuery?.OrganizationIds == null || organizationIdQuery.OrganizationIds.Count <= 0)
                return;

            if (organizationIdQuery.OrganizationIds.Count == 1)
                container &= Filter<T>.Term("organization", organizationIdQuery.OrganizationIds.First());
            else
                container &= Filter<T>.Terms("organization", organizationIdQuery.OrganizationIds.ToArray());
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
