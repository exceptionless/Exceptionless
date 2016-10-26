using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IOrganizationIdQuery {
        List<string> OrganizationIds { get; }
    }

    public class OrganizationIdQueryBuilder : IElasticQueryBuilder {
        private readonly string _organizationIdFieldName;

        public OrganizationIdQueryBuilder() {
            _organizationIdFieldName = nameof(IOwnedByOrganization.OrganizationId).ToLowerUnderscoredWords();
        }

        public void Build<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var organizationIdQuery = ctx.GetSourceAs<IOrganizationIdQuery>();
            if (organizationIdQuery?.OrganizationIds == null || organizationIdQuery.OrganizationIds.Count <= 0)
                return;

            if (organizationIdQuery.OrganizationIds.Count == 1)
                ctx.Query &= Query<T>.Term(_organizationIdFieldName, organizationIdQuery.OrganizationIds.First());
            else
                ctx.Query &= Query<T>.Terms(t => t.Field(_organizationIdFieldName).Terms(organizationIdQuery.OrganizationIds));
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
