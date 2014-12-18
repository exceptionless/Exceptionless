using System;

namespace Exceptionless.Core.Messaging.Models {
    public enum ChangeType {
        Added,
        Saved,
        Removed,
        RemovedAll,
        UpdatedAll
    }
}