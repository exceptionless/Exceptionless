using System;
using Nest;

namespace Exceptionless.Core.Extensions {
    public static class ElasticExtensions {
        public static TextPropertyDescriptor<T> AddKeywordField<T>(this TextPropertyDescriptor<T> descriptor) where T : class {
            return descriptor.Fields(f => f.Keyword(s => s.Name("keyword").IgnoreAbove(256)));
        }
    }
}