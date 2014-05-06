using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Extensions {
    public static class MongoQueryExtensions {
        public static IMongoQuery And(this IMongoQuery query, params IMongoQuery[] queries) {
            var result = new List<IMongoQuery>(queries.Where(q => q != null));
            if (query != null)
                result.Add(query);

            return Query.And(result);
        }
    }
}
