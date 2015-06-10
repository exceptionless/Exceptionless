using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Repositories.Configuration;

namespace Exceptionless.Core.Extensions {
    public static class ElasticSearchIndexExtensions {
        public static IEnumerable<KeyValuePair<Type, string>> ToTypeIndices(this IEnumerable<IElasticSearchIndex> indexes) {
            return indexes.SelectMany(idx => idx.GetIndexTypes().Select(kvp => new KeyValuePair<Type, string>(kvp.Key, idx.VersionedName)));
        }
    }
}