using System;
using System.Web.Http.Routing.Constraints;

namespace Exceptionless.Api.Utility {
    public class IdentifiersRouteConstraint : RegexRouteConstraint {
        public IdentifiersRouteConstraint() : base(@"^[a-zA-Z\d-]{8,100}(,[a-zA-Z\d-]{8,100})*$") { }
    }
}