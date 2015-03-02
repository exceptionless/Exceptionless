using System;
using System.Linq;
using System.Linq.Expressions;
using CodeSmith.Core.Events;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class MultiOptions<T> : OneOptions<T> where T : IIdentity {
        public event EventHandler<EventArgs<bool>> HasMoreChanged;
        private bool _hasMore;

        public bool HasMore {
            get { return _hasMore; }
            set {
                _hasMore = value;
                if (HasMoreChanged != null)
                    HasMoreChanged(this, new EventArgs<bool>(_hasMore));
            }
        }

        public string BeforeValue { get; set; }
        public Expression<Func<T, bool>> BeforeQuery { get; set; }
        public string AfterValue { get; set; }
        public Expression<Func<T, bool>> AfterQuery { get; set; }
        public int? Limit { get; set; }
        public int? Page { get; set; }

        public bool UseLimit {
            get { return Limit.HasValue; }
        }

        public bool UseSkip {
            get { return UsePaging; }
        }

        public bool UsePaging {
            get { return Page.HasValue; }
        }

        public int GetLimit() {
            if (!Limit.HasValue || Limit.Value < 1)
                return RepositoryConstants.DEFAULT_LIMIT;

            if (Limit.Value > RepositoryConstants.MAX_LIMIT)
                return RepositoryConstants.MAX_LIMIT;

            return Limit.Value;
        }

        public int GetSkip() {
            if (!Page.HasValue || Page.Value < 1)
                return 0;

            int skip = (Page.Value - 1) * GetLimit();
            if (skip < 0)
                skip = 0;

            return skip;
        }

        public override IQueryable<T> ApplyFilter(IQueryable<T> query) {
            query = base.ApplyFilter(query);

            if (Page.HasValue)
                return query;

            if (!String.IsNullOrEmpty(BeforeValue) && BeforeQuery == null)
                BeforeQuery = d => String.Compare(d.Id, BeforeValue, StringComparison.Ordinal) == -1;
            
            if (!String.IsNullOrEmpty(AfterValue) && AfterQuery == null)
                AfterQuery = d => String.Compare(d.Id, AfterValue, StringComparison.Ordinal) == 1;

            if (BeforeQuery != null)
                query = query.Where(BeforeQuery);

            if (AfterQuery != null)
                query = query.Where(AfterQuery);

            return query;
        }
    }
}