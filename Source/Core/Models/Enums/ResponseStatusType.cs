using System;

namespace Exceptionless.Core.Models {
    public enum ResponseStatusType {
        Successful = 0,
        Queued = 1,
        Error = 2,
        Discarded = 3,
        Rejected = 4
    }
}