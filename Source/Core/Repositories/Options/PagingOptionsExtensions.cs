using System;

namespace Exceptionless.Core.Repositories {
    public static class PagingOptionsExtensions {
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