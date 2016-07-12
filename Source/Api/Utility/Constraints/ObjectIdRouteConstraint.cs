﻿using System;
using System.Web.Http.Routing.Constraints;

namespace Exceptionless.Api.Utility {
    public class ObjectIdRouteConstraint : RegexRouteConstraint {
        public ObjectIdRouteConstraint() : base(@"^[a-zA-Z\d]{24,36}$") {}
    }
}