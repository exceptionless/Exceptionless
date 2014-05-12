#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Mvc;
using System.Web.Routing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.App.Utility {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class NotSuspendedAttribute : ActionFilterAttribute {
        [Inject]
        public IOrganizationRepository OrganizationRepository { get; set; }

        [Inject]
        public IProjectRepository ProjectRepository { get; set; }

        public override void OnActionExecuted(ActionExecutedContext context) {
            string organizationId = context.RouteData.GetOrganizationId();
            string projectId = context.RouteData.GetProjectId();

            if (String.IsNullOrEmpty(organizationId) && String.IsNullOrEmpty(projectId))
                return;

            if (String.IsNullOrEmpty(organizationId)) {
                Project project = ProjectRepository.GetById(projectId, true);
                if (project == null)
                    return;

                organizationId = project.OrganizationId;
            }

            Organization organization = OrganizationRepository.GetById(organizationId, true);
            if (organization == null)
                return;

            if (!organization.IsSuspended)
                return;

            context.Result = new RedirectToRouteResult(new RouteValueDictionary {
                { "Controller", "Organization" },
                { "Action", "Suspended" },
                { "Id", organizationId }
            });
        }
    }

    internal static class RouteDataExtensions {
        public static void SetOrganizationId(this RouteData routeData, string organizationId) {
            routeData.Values.Add("OrganizationId", organizationId);
        }

        public static string GetOrganizationId(this RouteData routeData) {
            if (routeData.Values.ContainsKey("OrganizationId"))
                return routeData.Values["OrganizationId"] as string;

            return String.Empty;
        }

        public static void SetProjectId(this RouteData routeData, string projectId) {
            routeData.Values.Add("ProjectId", projectId);
        }

        public static string GetProjectId(this RouteData routeData) {
            if (routeData.Values.ContainsKey("ProjectId"))
                return routeData.Values["ProjectId"] as string;

            return String.Empty;
        }
    }
}