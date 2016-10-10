using System;

namespace Exceptionless.Core.Models {
    public enum SuspensionCode {
        Billing,
        Overage,
        Abuse,
        Other = 100
    }
}