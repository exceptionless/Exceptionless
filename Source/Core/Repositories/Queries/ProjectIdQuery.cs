using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Elasticsearch.Repositories.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IProjectIdQuery {
        List<string> ProjectIds { get; }
    }

    public class ProjectIdQueryBuilder : QueryBuilderBase {
        public override void BuildFilter<T>(object query, object options, ref QueryContainer container) {
            var projectIdQuery = query as IProjectIdQuery;
            if (projectIdQuery?.ProjectIds == null || projectIdQuery.ProjectIds.Count <= 0)
                return;

            container &= Query<T>.Terms(t => t.Field("project").Terms(projectIdQuery.ProjectIds.ToArray()));
        }
    }

    public static class ProjectIdQueryExtensions {
        public static T WithProjectId<T>(this T query, string id) where T : IProjectIdQuery {
            if (!String.IsNullOrEmpty(id))
                query.ProjectIds.Add(id);
            return query;
        }

        public static T WithProjectIds<T>(this T query, params string[] ids) where T : IProjectIdQuery {
            query.ProjectIds.AddRange(ids.Distinct());
            return query;
        }

        public static T WithProjectIds<T>(this T query, IEnumerable<string> ids) where T : IProjectIdQuery {
            query.ProjectIds.AddRange(ids.Distinct());
            return query;
        }
    }
}
