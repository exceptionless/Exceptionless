using System;
using Microsoft.AspNetCore.Routing.Constraints;

namespace Exceptionless.Api.Utility {
    public class TokensRouteConstraint : RegexRouteConstraint {
        public TokensRouteConstraint() : base(@"^[a-zA-Z\d]{24,40}(,[a-zA-Z\d]{24,40})*$") { }
    }
}