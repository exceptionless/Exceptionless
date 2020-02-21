using System.Collections.Generic;

namespace Exceptionless.Web.Controllers {
    public class WorkInProgressResult {
        public WorkInProgressResult() {
            Workers = new List<string>();
        }

        public WorkInProgressResult(IEnumerable<string> workers) : this() {
            Workers.AddRange(workers ?? new List<string>());
        }

        public List<string> Workers { get; set; }
    }
}