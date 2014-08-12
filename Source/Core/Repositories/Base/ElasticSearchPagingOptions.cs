using System;
using Nest;

namespace Exceptionless.Core.Repositories {
    internal class ElasticSearchPagingOptions<T> : PagingWtihBeforeAfterSortByOptions<QueryContainer, Func<SortFieldDescriptor<T>, IFieldSort>> where T : class {
         public ElasticSearchPagingOptions(PagingOptions options) : base(options) {}
    }
}