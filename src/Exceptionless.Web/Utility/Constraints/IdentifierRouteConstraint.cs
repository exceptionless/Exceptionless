using Microsoft.AspNetCore.Routing.Constraints;

namespace Exceptionless.Web.Utility {
    public class IdentifierRouteConstraint : RegexRouteConstraint {
        public IdentifierRouteConstraint() : base(@"^[a-zA-Z\d-]{8,100}$") { }
    }
}