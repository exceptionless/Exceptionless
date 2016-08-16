using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IStackIdQuery {
        List<string> StackIds { get; }
    }

    public class StackIdQueryBuilder : IElasticQueryBuilder {
        public void Build<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var stackIdQuery = ctx.GetSourceAs<IStackIdQuery>();
            if (stackIdQuery?.StackIds == null || stackIdQuery.StackIds.Count <= 0)
                return;

            if (stackIdQuery.StackIds.Count == 1)
                ctx.Filter &= Filter<T>.Term("stack", stackIdQuery.StackIds.First());
            else
                ctx.Filter &= Filter<T>.Terms("stack", stackIdQuery.StackIds.ToArray());
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
