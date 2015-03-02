using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation {
    public class ExceptionlessClientCoreLastReferenceIdManager : ICoreLastReferenceIdManager {
        public string GetLastReferenceId() {
            return ExceptionlessClient.Default.GetLastReferenceId();
        }
    }
}
