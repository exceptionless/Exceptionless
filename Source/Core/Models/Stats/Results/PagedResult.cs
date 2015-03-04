using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class PagedResult<T> where T : class {
        public PagedResult(List<T> results = null, long? totalCount = null) {
            Results = results ?? new List<T>();
            TotalCount = totalCount;
        }

        public List<T> Results { get; set; }
        public long? TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}