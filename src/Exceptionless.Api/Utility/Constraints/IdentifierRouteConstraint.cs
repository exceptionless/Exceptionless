using System;
using System.Web.Http.Routing.Constraints;

namespace Exceptionless.Api.Utility {
    public class IdentifierRouteConstraint : RegexRouteConstraint {
        public IdentifierRouteConstraint() : base(@"^[a-zA-Z\d-]{8,100}$") { }
    }
}