using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Repositories;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IStackIdQuery {
        List<string> StackIds { get; }
    }

    public class StackIdQueryBuilder : QueryBuilderBase {
        public override void BuildFilter<T>(IReadOnlyRepository<T> repository, FilterContainer container, object query) {
            var stackIdQuery = query as IStackIdQuery;
            if (stackIdQuery == null || stackIdQuery.StackIds.Count <= 0)
                return;

            if (stackIdQuery.StackIds.Count == 1)
                container &= Filter<T>.Term("stack", stackIdQuery.StackIds.First());
            else
                container &= Filter<T>.Terms("stack", stackIdQuery.StackIds.ToArray());
        }
    }

    public static class StackIdQueryExtensions {
        public static T WithStackId<T>(this T query, string id) where T : IStackIdQuery {
            if (!String.IsNullOrEmpty(id))
                query.StackIds.Add(id);
            return query;
        }

        public static T WithStackIds<T>(this T query, params string[] ids) where T : IStackIdQuery {
            query.StackIds.AddRange(ids.Distinct());
            return query;
        }

        public static T WithStackIds<T>(this T query, IEnumerable<string> ids) where T : IStackIdQuery {
            query.StackIds.AddRange(ids.Distinct());
            return query;
        }
    }
}
