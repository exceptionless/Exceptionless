using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Elasticsearch.Repositories.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IStackIdQuery {
        List<string> StackIds { get; }
    }

    public class StackIdQueryBuilder : QueryBuilderBase {
        public override void BuildFilter<T>(object query, object options, ref QueryContainer container) {
            var stackIdQuery = query as IStackIdQuery;
            if (stackIdQuery?.StackIds == null || stackIdQuery.StackIds.Count <= 0)
                return;

            container &= Query<T>.Terms(t => t.Field("stack").Terms(stackIdQuery.StackIds.ToArray()));
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
