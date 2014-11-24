using System;
using System.Collections.Generic;
using Nest;

namespace Exceptionless.Core.Repositories {
    internal class ElasticSearchPagingOptions<T> : PagingWitSortByOptions<FilterContainer, List<Func<SortFieldDescriptor<T>, IFieldSort>>> where T : class {
        public ElasticSearchPagingOptions(PagingOptions options) : base(options) {
            SortBy = new List<Func<SortFieldDescriptor<T>, IFieldSort>>();
        }
    }
}