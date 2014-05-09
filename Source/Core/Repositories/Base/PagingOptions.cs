using System;

namespace Exceptionless.Core.Repositories {
    public class PagingOptions {
        public string Before { get; set; }
        public string After { get; set; }
        public int? Limit { get; set; }
        public int? Page { get; set; }
        public bool HasMore { get; set; }
    }
}
