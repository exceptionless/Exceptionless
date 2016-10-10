using System;

namespace Exceptionless.Core.Utility {
    public class NullCoreLastReferenceIdManager : ICoreLastReferenceIdManager {
        public string GetLastReferenceId() {
            return null;
        }
    }

    public interface ICoreLastReferenceIdManager {
        string GetLastReferenceId();
    }
}
