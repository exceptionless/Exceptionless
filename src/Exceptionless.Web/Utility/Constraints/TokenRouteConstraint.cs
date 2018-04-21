using System;
using Microsoft.AspNetCore.Routing.Constraints;

namespace Exceptionless.Api.Utility {
    public class TokenRouteConstraint : RegexRouteConstraint {
        public TokenRouteConstraint() : base(@"^[a-zA-Z\d]{24,40}$") { }
    }
}