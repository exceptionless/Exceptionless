using Microsoft.AspNetCore.Routing.Constraints;

namespace Exceptionless.Web.Utility {
    public class TokenRouteConstraint : RegexRouteConstraint {
        public TokenRouteConstraint() : base(@"^[a-zA-Z\d]{24,40}$") { }
    }
}