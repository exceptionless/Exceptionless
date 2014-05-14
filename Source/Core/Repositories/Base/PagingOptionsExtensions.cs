using System;

namespace Exceptionless.Core.Repositories {
    public static class PagingOptionsExtensions {
        public static PagingOptions WithBefore(this PagingOptions options, string before) {
            options.Before = before;
            return options;
        }

        public static PagingOptions WithAfter(this PagingOptions options, string after) {
            options.After = after;
            return options;
        }
        
        public static PagingOptions WithLimit(this PagingOptions options, int? limit) {
            options.Limit = limit;
            return options;
        }

        public static PagingOptions WithPage(this PagingOptions options, int? page) {
            options.Page = page;
            return options;
        }
    }
}