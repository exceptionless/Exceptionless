using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation {
    public class ExceptionlessClientCoreLastReferenceIdManager : ICoreLastReferenceIdManager {
        private readonly ExceptionlessClient _client;

        public ExceptionlessClientCoreLastReferenceIdManager(ExceptionlessClient client) {
            _client = client;
        }

        public string GetLastReferenceId() {
            return _client.GetLastReferenceId();
        }
    }
}
