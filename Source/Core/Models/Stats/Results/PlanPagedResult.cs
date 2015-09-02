using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Stats {
    public class PlanPagedResult<T> : PagedResult<T> where T : class {
        public PlanPagedResult(List<T> results = null, bool isLimitedByPlan = false, long? totalLimitedByPlan = null, long? totalCount = null) : base(results, totalCount) {
            TotalLimitedByPlan = totalLimitedByPlan;
            IsLimitedByPlan = isLimitedByPlan || totalLimitedByPlan.HasValue && totalLimitedByPlan > 0;
        }

        private long? _totalLimitedByPlan;

        public long? TotalLimitedByPlan {
            get { return _totalLimitedByPlan; }
            set {
                _totalLimitedByPlan = value;
                IsLimitedByPlan = _totalLimitedByPlan > 0;
            }
        }

        public bool IsLimitedByPlan { get; set; }
    }
}