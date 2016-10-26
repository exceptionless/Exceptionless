using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IProjectIdQuery {
        List<string> ProjectIds { get; }
    }

    public class ProjectIdQueryBuilder : IElasticQueryBuilder {
        private readonly string _projectIdFieldName;

        public ProjectIdQueryBuilder() {
            _projectIdFieldName = nameof(IOwnedByProject.ProjectId).ToLowerUnderscoredWords();
        }

        public void Build<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var projectIdQuery = ctx.GetSourceAs<IProjectIdQuery>();
            if (projectIdQuery?.ProjectIds == null || projectIdQuery.ProjectIds.Count <= 0)
                return;

            if (projectIdQuery.ProjectIds.Count == 1)
                ctx.Query &= Query<T>.Term(_projectIdFieldName, projectIdQuery.ProjectIds.First());
            else
                ctx.Query &= Query<T>.Terms(t => t.Field(_projectIdFieldName).Terms(projectIdQuery.ProjectIds));
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
