using System;

namespace Exceptionless.Core.Repositories {
    internal class PagingWithBeforeAfterSortByOptions<TQuery, TSortBy> : PagingOptions {
        private readonly PagingOptions _options;

        public PagingWithBeforeAfterSortByOptions(PagingOptions options) {
            _options = options ?? new PagingOptions();
        }

        public TQuery BeforeQuery { get; set; }
        public TQuery AfterQuery { get; set; }
        public TSortBy SortBy { get; set; }

        public override string Before {
            get { return _options.Before; }
            set { _options.Before = value; }
        }

        public override string After {
            get { return _options.After; }
            set { _options.After = value; }
        }

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