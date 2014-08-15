using System;
using System.Collections.Generic;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ElasticSearchOptions<T> : MultiOptions where T : class {
        public ElasticSearchOptions() {
            SortBy = new List<Func<SortFieldDescriptor<T>, IFieldSort>>();
        }

        public List<Func<SortFieldDescriptor<T>, IFieldSort>> SortBy { get; private set; }
        public QueryContainer BeforeQuery { get; set; }
        public QueryContainer AfterQuery { get; set; }
        public QueryContainer Query { get; set; }
    }
}