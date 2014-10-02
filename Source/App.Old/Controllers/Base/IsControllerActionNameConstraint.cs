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
using MvcActionNameAttribute = System.Web.Mvc.ActionNameAttribute;
using HttpActionNameAttribute = System.Web.Http.ActionNameAttribute;

namespace Exceptionless.App.Controllers {
    public class IsControllerActionNameConstraint : IRouteConstraint, IHttpRouteConstraint {
        private readonly Dictionary<string, ISet<string>> _controllerActions = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);

        public IsControllerActionNameConstraint(string controllerName = null, IEnumerable<Assembly> assemblies = null) {
            ControllerName = controllerName;

            var assembliesToCheck = new List<Assembly>(assemblies ?? new Assembly[] { });
            if (assembliesToCheck.Count == 0)
                assembliesToCheck.Add(Assembly.GetCallingAssembly());

            foreach (Assembly assemblyToCheck in assembliesToCheck) {
                IEnumerable<Type> controllers = assemblyToCheck.GetTypes().Where(type => typeof(IController).IsAssignableFrom(type) || typeof(IHttpController).IsAssignableFrom(type));
                foreach (Type controller in controllers) {
                    if (controllerName != null && !controller.Name.StartsWith(controllerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    IEnumerable<string> actionMethods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")).Select(m => {
                        if (controller is IController) {
                            var actionNameAttr = m.GetCustomAttribute(typeof(MvcActionNameAttribute)) as MvcActionNameAttribute;
                            if (actionNameAttr != null && !String.IsNullOrWhiteSpace(actionNameAttr.Name))
                                return actionNameAttr.Name;
                        } else {
                            var httpActionNameAttr = m.GetCustomAttribute(typeof(HttpActionNameAttribute)) as HttpActionNameAttribute;
                            if (httpActionNameAttr != null && !String.IsNullOrWhiteSpace(httpActionNameAttr.Name))
                                return httpActionNameAttr.Name;
                        }

                        return m.Name;
                    });

                    string name = controller.Name.Replace("Controller", "");
                    if (!_controllerActions.ContainsKey(name))
                        _controllerActions.Add(name, new HashSet<string>());

                    _controllerActions[name].AddRange(actionMethods);
                }
            }
        }

        public string ControllerName { get; private set; }

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

            string controllerName = ControllerName ?? values["controller"] as string;
            if (controllerName == null)
                return false;

            if (!_controllerActions.ContainsKey(controllerName))
                return false;

            return _controllerActions[controllerName].Contains(parameterValue, StringComparer.OrdinalIgnoreCase);
        }
    }
}