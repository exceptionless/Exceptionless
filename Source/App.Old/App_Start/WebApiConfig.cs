#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net.Http.Formatting;
using System.Web.Http;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Utility;
using Exceptionless.App.Controllers;
using Exceptionless.Core;
using Exceptionless.Core.Caching;

namespace Exceptionless.App {
    public static class WebApiConfig {
        public const string SERVICE_URL_VERISON1 = "api/v1/";

        public static void Register(HttpConfiguration config) {
            config.MessageHandlers.Add(new XHttpMethodOverrideDelegatingHandler());
            config.MessageHandlers.Add(new EncodingDelegatingHandler());

            // Throttle api calls to X every 15 minutes by IP address.
            var cacheClient = config.DependencyResolver.GetService(typeof(ICacheClient)) as ICacheClient;
            config.MessageHandlers.Add(new ThrottlingHandler(cacheClient, userIdentifier => Settings.Current.ApiThrottleLimit, TimeSpan.FromMinutes(15)));

            config.Formatters.Clear();
            //config.Formatters.Remove(config.Formatters.JsonFormatter);
            config.Formatters.Add(new UpgradableJsonMediaTypeFormatter());
            config.Services.Replace(typeof(IContentNegotiator), new JsonContentNegotiator());

            // Log all api usages.
            //config.MessageHandlers.Add(new UsageHandler());
            //config.EnableSystemDiagnosticsTracing();

            config.Routes.MapHttpRouteLowercase(
                "By Project",
                String.Concat(SERVICE_URL_VERISON1, "{controller}/project/{projectId}/{action}"),
                new { action = "GetByProject" },
                new { controller = new IsControllerNameConstraint(), projectId = @"^[a-zA-Z\d]{24}$" }
            );

            config.Routes.MapHttpRouteLowercase(
                "By Organization",
                String.Concat(SERVICE_URL_VERISON1, "{controller}/organization/{organizationId}"),
                new { action = "GetByOrganizationId" },
                new { controller = new IsControllerNameConstraint(), organizationId = @"^[a-zA-Z\d]{24}$" }
            );

            config.Routes.MapHttpRouteLowercase(
                "By Stack",
                String.Concat(SERVICE_URL_VERISON1, "{controller}/stack/{stackId}/{action}"),
                new { action = "GetByStack" },
                new { controller = new IsControllerNameConstraint(), stackId = @"^[a-zA-Z\d]{24}$" }
            );

            config.Routes.MapHttpRouteLowercase(
                "User Notifications",
                String.Concat(SERVICE_URL_VERISON1, "project/{id}/notification/{userId}"),
                new { controller = "Project", action = "Notification" },
                new { id = @"^[a-zA-Z\d]{24}$", userId = @"^[a-zA-Z\d]{24}$" }
            );

            config.Routes.MapHttpRouteLowercase(
                "Get Project Api Key",
                String.Concat(SERVICE_URL_VERISON1, "project/{projectId}/get-key"),
                new { controller = "Project", action = "GetOrAddKey" },
                new { projectId = @"^[a-zA-Z\d]{24}$" }
            );

            config.Routes.MapHttpRouteLowercase(
                "Project Api Keys",
                String.Concat(SERVICE_URL_VERISON1, "project/{projectId}/key/{apiKey}"),
                new { controller = "Project", action = "ManageApiKeys", apiKey = RouteParameter.Optional },
                new { projectId = @"^[a-zA-Z\d]{24}$" }
            );

            config.Routes.MapHttpRouteLowercase(
                "Middle Api Id",
                String.Concat(SERVICE_URL_VERISON1, "{controller}/{id}/{action}"),
                new { },
                new { controller = new IsControllerNameConstraint(), id = @"^[a-zA-Z\d]{24}$", action = new IsControllerActionNameConstraint() }
            );

            config.Routes.MapHttpRouteLowercase(
                "Default Api With Action",
                String.Concat(SERVICE_URL_VERISON1, "{controller}/{action}/{id}"),
                new { id = RouteParameter.Optional },
                new { controller = new IsControllerNameConstraint(), action = new IsControllerActionNameConstraint() }
            );

            config.Routes.MapHttpRouteLowercase(
                "DefaultApi",
                String.Concat(SERVICE_URL_VERISON1, "{controller}/{id}"),
                new { id = RouteParameter.Optional }
            );
        }
    }
}