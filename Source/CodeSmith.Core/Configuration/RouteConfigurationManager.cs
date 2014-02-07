using System;
using System.Collections.Generic;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Routing;

namespace CodeSmith.Core.Configuration
{
    /// <summary>
    /// A class to add routes to the <see cref="RouteTable"/> from the web.config file.
    /// </summary>
    public static class RouteConfigurationManager
    {
        /// <summary>
        /// Registers the routes in the web.config file.
        /// </summary>
        public static void RegisterRoutes()
        {
            RegisterRoutes(false);
        }

        /// <summary>
        /// Registers the routes in the web.config file.
        /// </summary>
        /// <param name="forceLowercase">Forces the routes to be lower case.</param>
        public static void RegisterRoutes(bool forceLowercase)
        {
            RegisterRoutes(RouteTable.Routes, forceLowercase);
        }

        /// <summary>
        /// Registers the routes in the web.config file.
        /// </summary>
        /// <param name="routes">The <see cref="RouteCollection"/> to add routes to.</param>
        public static void RegisterRoutes(RouteCollection routes)
        {
            RegisterRoutes(routes, false);
        }

        /// <summary>
        /// Registers the routes in the web.config file.
        /// </summary>
        /// <param name="routes">The <see cref="RouteCollection"/> to add routes to.</param>
        /// <param name="forceLowercase">Forces the routes to be lower case.</param>
        public static void RegisterRoutes(RouteCollection routes, bool forceLowercase)
        {
            RouteTableSection routesTableSection = GetRouteTableConfigurationSection();

            if (routesTableSection == null || routesTableSection.Routes.Count < 1)
                return;

            for (int i = 0; i < routesTableSection.Routes.Count; i++)
            {
                RouteElement routeElement = routesTableSection.Routes[i];

                Route route;

                if (forceLowercase)
                    route = new LowercaseRoute(
                        routeElement.Url,
                        GetDefaults(routeElement),
                        GetConstraints(routeElement),
                        GetDataTokens(routeElement),
                        GetInstanceOfRouteHandler(routeElement));
                else
                    route = new Route(
                        routeElement.Url,
                        GetDefaults(routeElement),
                        GetConstraints(routeElement),
                        GetDataTokens(routeElement),
                        GetInstanceOfRouteHandler(routeElement));

                routes.Add(routeElement.Name, route);
            }
        }

        private static RouteTableSection GetRouteTableConfigurationSection()
        {
            RouteTableSection routesTableSection;

            try
            {
                routesTableSection = (RouteTableSection) WebConfigurationManager.GetSection("routeTable");
                return routesTableSection;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Can't find section <routeTable> in the configuration file.", ex);
            }
        }

        private static IRouteHandler GetInstanceOfRouteHandler(RouteElement route)
        {
            IRouteHandler routeHandler;

            if (string.IsNullOrEmpty(route.RouteHandlerType))
                return new MvcRouteHandler();

            try
            {
                Type routeHandlerType = Type.GetType(route.RouteHandlerType);
                routeHandler = Activator.CreateInstance(routeHandlerType) as IRouteHandler;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(
                    string.Format("Can't create an instance of IRouteHandler {0}", route.RouteHandlerType),
                    ex);
            }

            return routeHandler;
        }

        private static RouteValueDictionary GetConstraints(RouteElement route)
        {
            return GetDictionary(route.Constraints.Attributes);
        }

        private static RouteValueDictionary GetDefaults(RouteElement route)
        {
            return GetDictionary(route.Defaults.Attributes);
        }

        private static RouteValueDictionary GetDataTokens(RouteElement route)
        {
            return GetDictionary(route.DataTokens.Attributes);
        }

        private static RouteValueDictionary GetDictionary(IDictionary<string, object> attributes)
        {
            if (attributes == null || attributes.Count < 1)
                return null;

            var data = new RouteValueDictionary();

            foreach (var attribte in attributes)
                data.Add(attribte.Key, attribte.Value);

            return data;
        }
    }
}