using System;
using System.Collections.Generic;

namespace CodeSmith.Core.Scheduler {
    public class JobRunContext : MarshalByRefObject {
        private readonly Action<string> _updateStatus;

        public JobRunContext(Action<string> updateStatus = null) {
            _updateStatus = updateStatus;
            Properties = new Dictionary<string, object>();
        }

        public void UpdateStatus(string message) {
            if (_updateStatus != null)
                _updateStatus(message);
        }

        public IDictionary<string, object> Properties { get; private set; } 

        public static readonly JobRunContext Default = new JobRunContext();
    }
}