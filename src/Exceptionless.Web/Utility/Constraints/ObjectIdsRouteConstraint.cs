﻿using Microsoft.AspNetCore.Routing.Constraints;

namespace Exceptionless.Web.Utility;

public class ObjectIdsRouteConstraint : RegexRouteConstraint
{
    public ObjectIdsRouteConstraint() : base(@"^[a-zA-Z\d]{24,36}(,[a-zA-Z\d]{24,36})*$") { }
}
