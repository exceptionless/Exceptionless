using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Repositories.Queries {
    public interface IStackIdQuery {
        List<string> StackIds { get; }
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