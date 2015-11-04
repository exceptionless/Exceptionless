using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IProjectIdQuery {
        List<string> ProjectIds { get; }
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