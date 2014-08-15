using System;

namespace Exceptionless.Core.Repositories {
    public class PagingOptions {
        public virtual string Before { get; set; }
        public virtual string After { get; set; }
        public virtual int? Limit { get; set; }
        public virtual int? Page { get; set; }
        public virtual bool HasMore { get; set; }
    }
}