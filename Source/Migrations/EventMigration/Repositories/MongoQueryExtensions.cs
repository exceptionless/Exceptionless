using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.EventMigration.Repositories {
    public static class MongoQueryExtensions {
        public static IMongoQuery And(this IMongoQuery query, params IMongoQuery[] queries) {
            var mongoQueries = new List<IMongoQuery>(queries.Where(q => q != null));
            if (query != null)
                mongoQueries.Add(query);

            if (mongoQueries.Count > 0)
                query = Query.And(mongoQueries);

            return query;
        }
    }
}