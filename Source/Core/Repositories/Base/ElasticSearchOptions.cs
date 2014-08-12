using System;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class ElasticSearchOptions<T> : MultiOptions where T : class {
        public Func<SortFieldDescriptor<T>, IFieldSort> SortBy { get; set; }
        public QueryContainer BeforeQuery { get; set; }
        public QueryContainer AfterQuery { get; set; }
        public QueryContainer Query { get; set; }
    }
}