using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Repositories.Configuration;

namespace Exceptionless.Core.Extensions {
    public static class ElasticSearchIndexExtensions {
        public static IEnumerable<KeyValuePair<Type, string>> GetTypeIndices(this IEnumerable<IElasticSearchIndex> indexes) {
            return indexes.SelectMany(idx => idx.GetIndexTypeNames().Select(kvp => new KeyValuePair<Type, string>(kvp.Key, idx.VersionedName)));
        }

        public static IEnumerable<KeyValuePair<Type, string>> GetIndexTypeNames(this IEnumerable<IElasticSearchIndex> indexes) {
            return indexes.SelectMany(idx => idx.GetIndexTypeNames());
        }
    }
}