using Microsoft.AspNetCore.Routing.Constraints;

namespace Exceptionless.Web.Utility {
    public class ObjectIdRouteConstraint : RegexRouteConstraint {
        public ObjectIdRouteConstraint() : base(@"^[a-zA-Z\d]{24,36}$") {}
    }
}