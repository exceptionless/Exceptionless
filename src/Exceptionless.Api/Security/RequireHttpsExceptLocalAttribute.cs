using System;

namespace Exceptionless.Api.Security {
    public sealed class RequireHttpsExceptLocalAttribute : RequireHttpsAttribute {
        public RequireHttpsExceptLocalAttribute() {
            IgnoreLocalRequests = true;
        }
    }
}