#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Routing;

namespace Exceptionless.App.Utility {
    internal class LowercaseRoute : Route {
        public LowercaseRoute(string url, IRouteHandler routeHandler)
            : base(url, routeHandler) {}

        public LowercaseRoute(string url, RouteValueDictionary defaults, IRouteHandler routeHandler)
            : base(url, defaults, routeHandler) {}

        public LowercaseRoute(string url, RouteValueDictionary defaults, RouteValueDictionary constraints, IRouteHandler routeHandler)
            : base(url, defaults, constraints, routeHandler) {}

        public LowercaseRoute(string url, RouteValueDictionary defaults, RouteValueDictionary constraints, RouteValueDictionary dataTokens, IRouteHandler routeHandler)
            : base(url, defaults, constraints, dataTokens, routeHandler) {}

        public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values) {
            VirtualPathData path = base.GetVirtualPath(requestContext, values);

            if (path != null) {
                string virtualPath = path.VirtualPath;
                int lastIndexOf = virtualPath.LastIndexOf("?");

                if (lastIndexOf != 0) {
                    if (lastIndexOf > 0) {
                        string leftPart = virtualPath.Substring(0, lastIndexOf).ToLowerInvariant();
                        string queryPart = virtualPath.Substring(lastIndexOf);
                        path.VirtualPath = leftPart + queryPart;
                    } else
                        path.VirtualPath = path.VirtualPath.ToLowerInvariant();
                }
            }

            return path;
        }
    }
}