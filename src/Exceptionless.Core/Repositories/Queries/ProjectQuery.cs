using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ProjectQueryExtensions {
        internal const string ProjectsKey = "@Projects";

        public static T Project<T>(this T query, string projectId) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(ProjectsKey, projectId);
        }
    }
}

namespace Exceptionless.Core.Repositories.Options {
    public static class ReadProjectQueryExtensions {
        public static ICollection<string> GetProjects(this IRepositoryQuery query) {
            return query.SafeGetCollection<string>(ProjectQueryExtensions.ProjectsKey);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries {
    public class ProjectQueryBuilder : IElasticQueryBuilder {
        private readonly string _projectIdFieldName;

        public ProjectQueryBuilder() {
            _projectIdFieldName = nameof(IOwnedByProject.ProjectId).ToLowerUnderscoredWords();
        }

        public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var projectIds = ctx.Source.GetProjects();
            if (projectIds.Count <= 0)
                return Task.CompletedTask;

            if (projectIds.Count == 1)
                ctx.Filter &= Query<T>.Term(_projectIdFieldName, projectIds.Single());
            else
                ctx.Filter &= Query<T>.Terms(d => d.Field(_projectIdFieldName).Terms(projectIds));

            return Task.CompletedTask;
        }
    }
}