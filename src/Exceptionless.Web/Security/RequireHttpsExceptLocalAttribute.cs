using System;

namespace Exceptionless.Web.Security {
    public sealed class RequireHttpsExceptLocalAttribute : RequireHttpsAttribute {
        public RequireHttpsExceptLocalAttribute() {
            IgnoreLocalRequests = true;
        }
    }
}