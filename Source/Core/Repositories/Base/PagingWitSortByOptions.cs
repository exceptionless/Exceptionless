using System;

namespace Exceptionless.Core.Repositories {
    internal class PagingWitSortByOptions<TQuery, TSortBy> : PagingOptions {
        private readonly PagingOptions _options;

        public PagingWitSortByOptions(PagingOptions options) {
            _options = options ?? new PagingOptions();
        }

        public TSortBy SortBy { get; set; }

        public override int? Limit {
            get { return _options.Limit; }
            set { _options.Limit = value; }
        }

        public override int? Page {
            get { return _options.Page; }
            set { _options.Page = value; }
        }

        public override bool HasMore {
            get { return _options.HasMore; }
            set { _options.HasMore = value; }
        }
    }
}