using System;
using System.Linq.Expressions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    internal class PagingWithSortingOptions<T> : PagingOptions where T: IIdentity {
        private readonly PagingOptions _options;

        public PagingWithSortingOptions(PagingOptions options) {
            _options = options;
        }

        public Expression<Func<T, bool>> BeforeQuery { get; set; }
        public Expression<Func<T, bool>> AfterQuery { get; set; }
        public Expression SortBy { get; set; }

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