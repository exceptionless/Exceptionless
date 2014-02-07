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
using System.ComponentModel;
using System.Web.Http;
using System.Web.Http.Routing;
using Exceptionless.Core.Web;

namespace Exceptionless.Core.Extensions {
    /// <summary>
    /// Contains extension methods to map HTTP routes to lowercase URLs.
    /// </summary>
    public static class HttpRouteCollectionExtensions {
        /// <summary>
        /// Maps the specified route template.
        /// </summary>
        /// <returns>
        /// A reference to the mapped route.
        /// </returns>
        /// <param name="routes">A collection of routes for the application.</param>
        /// <param name="name">The name of the route to map.</param>
        /// <param name="routeTemplate">The route template for the route.</param>
        public static IHttpRoute MapHttpRouteLowercase(this HttpRouteCollection routes, string name, string routeTemplate) {
            return MapHttpRouteLowercase(routes, name, routeTemplate, null, null);
        }

        /// <summary>
        /// Maps the specified route template and sets default constraints.
        /// </summary>
        /// <returns>
        /// A reference to the mapped route.
        /// </returns>
        /// <param name="routes">A collection of routes for the application.</param>
        /// <param name="name">The name of the route to map.</param>
        /// <param name="routeTemplate">The route template for the route.</param>
        /// <param name="defaults">An object that contains default route values.</param>
        public static IHttpRoute MapHttpRouteLowercase(this HttpRouteCollection routes, string name, string routeTemplate, object defaults) {
            return MapHttpRouteLowercase(routes, name, routeTemplate, defaults, null);
        }

        /// <summary>
        /// Maps the specified route template and sets default route values and constraints.
        /// </summary>
        /// <returns>
        /// A reference to the mapped route.
        /// </returns>
        /// <param name="routes">A collection of routes for the application.</param>
        /// <param name="name">The name of the route to map.</param>
        /// <param name="routeTemplate">The route template for the route.</param>
        /// <param name="defaults">An object that contains default route values.</param>
        /// <param name="constraints">A set of expressions that specify values for routeTemplate.</param>
        public static IHttpRoute MapHttpRouteLowercase(this HttpRouteCollection routes, string name, string routeTemplate, object defaults, object constraints) {
            if (routes == null)
                throw new ArgumentNullException("routes");
            IHttpRoute route = CreateRoute(routeTemplate, GetTypeProperties(defaults), GetTypeProperties(constraints), new Dictionary<string, object>(), null);
            routes.Add(name, route);
            return route;
        }

        private static IHttpRoute CreateRoute(string routeTemplate, IDictionary<string, object> defaults, IDictionary<string, object> constraints, IDictionary<string, object> dataTokens, IDictionary<string, object> parameters) {
            HttpRouteValueDictionary defaults1 = defaults != null ? new HttpRouteValueDictionary(defaults) : null;
            HttpRouteValueDictionary constraints1 = constraints != null ? new HttpRouteValueDictionary(constraints) : null;
            HttpRouteValueDictionary dataTokens1 = dataTokens != null ? new HttpRouteValueDictionary(dataTokens) : null;
            return new LowercaseHttpRoute(routeTemplate, defaults1, constraints1, dataTokens1);
        }

        private static IDictionary<string, object> GetTypeProperties(object instance) {
            var dictionary = new Dictionary<string, object>();
            if (instance != null) {
                foreach (PropertyDescriptor propertyDescriptor in TypeDescriptor.GetProperties(instance)) {
                    object obj = propertyDescriptor.GetValue(instance);
                    dictionary.Add(propertyDescriptor.Name, obj);
                }
            }

            return dictionary;
        }
    }
}