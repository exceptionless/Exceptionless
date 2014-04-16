using System;

namespace CodeSmith.Core.Scheduler {
    public class JobRunContext : MarshalByRefObject {
        private readonly Action<string> _updateStatus;

        public JobRunContext(Action<string> updateStatus = null) {
            _updateStatus = updateStatus;
        }

        public void UpdateStatus(string message) {
            if (_updateStatus != null)
                _updateStatus(message);
        }

        public static readonly JobRunContext Default = new JobRunContext();
    }
}