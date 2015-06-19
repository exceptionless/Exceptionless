using System;

namespace Exceptionless.EventMigration.Repositories {
    public class PagingOptions {
        public virtual int? Limit { get; set; }
        public virtual int? Page { get; set; }
        public virtual bool HasMore { get; set; }
    }
}