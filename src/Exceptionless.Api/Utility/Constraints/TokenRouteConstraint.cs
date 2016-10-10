using System;
using System.Web.Http.Routing.Constraints;

namespace Exceptionless.Api.Utility {
    public class TokenRouteConstraint : RegexRouteConstraint {
        public TokenRouteConstraint() : base(@"^[a-zA-Z\d]{24,40}$") { }
    }
}