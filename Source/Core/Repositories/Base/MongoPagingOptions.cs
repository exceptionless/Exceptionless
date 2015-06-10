using System;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    internal class MongoPagingOptions : PagingWithSortByOptions<IMongoQuery, IMongoSortBy> {
         public MongoPagingOptions(PagingOptions options) : base(options) {}
    }
}