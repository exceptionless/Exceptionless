using System;
using MongoDB.Driver;

namespace Exceptionless.EventMigration.Repositories {
    internal class MongoPagingOptions : PagingWitSortByOptions<IMongoQuery, IMongoSortBy> {
         public MongoPagingOptions(PagingOptions options) : base(options) {}
    }
}