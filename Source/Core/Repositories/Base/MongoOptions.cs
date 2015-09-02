using System;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class MongoOptions : MultiOptions {
        public IMongoSortBy SortBy { get; set; }
        public ReadPreference ReadPreference { get; set; }
        public IMongoQuery Query { get; set; }
    }
}