using System;
using CodeSmith.Core.Events;
using CodeSmith.Core.Extensions;

namespace Exceptionless.Core.Repositories {
    public class MultiOptions : OneOptions {
        public event EventHandler<EventArgs<bool>> HasMoreChanged;
        private bool _hasMore;
        public static readonly DateTime ServiceStartDate = new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public bool HasMore {
            get { return _hasMore; }
            set {
                _hasMore = value;
                if (HasMoreChanged != null)
                    HasMoreChanged(this, new EventArgs<bool>(_hasMore));
            }
        }

        public string BeforeValue { get; set; }
        public string AfterValue { get; set; }
        public int? Limit { get; set; }
        public int? Page { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string DateField { get; set; }

        public bool UseLimit {
            get { return Limit.HasValue; }
        }

        public bool UseSkip {
            get { return UsePaging; }
        }

        public bool UsePaging {
            get { return Page.HasValue; }
        }

        public bool UseDateRange {
            get { return !String.IsNullOrEmpty(DateField) && (StartDate.HasValue || EndDate.HasValue); }
        }

        public DateTime GetStartDate() {
            return StartDate.HasValue ? StartDate.Value : ServiceStartDate;
        }

        public DateTime GetEndDate() {
            return EndDate.HasValue ? EndDate.Value : DateTime.UtcNow.ToEndOfDay();
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
    }
}