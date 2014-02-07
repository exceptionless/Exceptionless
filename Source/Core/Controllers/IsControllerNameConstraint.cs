#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Routing;
using System.Web.Mvc;
using System.Web.Routing;
using CodeSmith.Core.Extensions;

namespace Exceptionless.Core.Controllers {
    public class IsControllerNameConstraint : IRouteConstraint, IHttpRouteConstraint {
        private readonly HashSet<string> _controllerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IsControllerNameConstraint(IEnumerable<Assembly> assemblies = null) {
            var assembliesToCheck = new List<Assembly>(assemblies ?? new Assembly[] { });
            if (assembliesToCheck.Count == 0)
                assembliesToCheck.Add(Assembly.GetCallingAssembly());

            foreach (Assembly assemblyToCheck in assembliesToCheck) {
                _controllerNames.AddRange(
                                          assemblyToCheck.GetTypes()
                                              .Where(type => typeof(IController).IsAssignableFrom(type) || typeof(IHttpController).IsAssignableFrom(type))
                                              .Select(t => t.Name.Replace("Controller", ""))
                    );
            }
        }

        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection) {
            return MatchInternal(parameterName, values);
        }

        public bool Match(HttpRequestMessage request, IHttpRoute route, string parameterName, IDictionary<string, object> values, HttpRouteDirection routeDirection) {
            return MatchInternal(parameterName, values);
        }

        private bool MatchInternal(string parameterName, IDictionary<string, object> values) {
            var parameterValue = values[parameterName] as string;
            if (String.IsNullOrEmpty(parameterValue))
                return false;

            return _controllerNames.Contains(parameterValue, StringComparer.OrdinalIgnoreCase);
        }
    }
}