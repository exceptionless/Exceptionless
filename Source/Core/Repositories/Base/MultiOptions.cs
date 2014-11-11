using System;
using CodeSmith.Core.Events;

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

        public bool UseStartDate {
            get { return StartDate.HasValue && StartDate.Value > ServiceStartDate; }
        }
        
        public bool UseEndDate {
            get { return EndDate.HasValue && EndDate.Value < DateTime.UtcNow.AddHours(1); }
        }

        public bool UseDateRange {
            get { return !String.IsNullOrEmpty(DateField) && (UseStartDate || UseEndDate); }
        }

        public DateTime GetStartDate() {
            return UseStartDate ? StartDate.GetValueOrDefault() : ServiceStartDate;
        }

        public DateTime GetEndDate() {
            return UseEndDate ? EndDate.GetValueOrDefault() : DateTime.UtcNow.AddHours(1);
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