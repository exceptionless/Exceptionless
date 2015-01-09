using System;

namespace Exceptionless.Core.Messaging.Models {
    public enum ChangeType : byte {
        Added = 0,
        Saved = 1,
        Removed = 2
    }
}