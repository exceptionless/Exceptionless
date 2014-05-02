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
using System.Data.Odbc;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Web.Http;
using Exceptionless.Models;

namespace Exceptionless.Core.Extensions {
    public static class HttpExtensions {
        public static User GetUser(this IPrincipal principal) {
            if (principal == null)
                return null;

            //if (Project != null)
            //    return String.Equals(Project.OrganizationId, organizationId);

            //if (UserEntity != null)
            //    return UserEntity.OrganizationIds.Contains(organizationId);

            return null;
        }

        public static string GetUserId(this IPrincipal principal) {
            if (principal == null)
                return null;

            //if (Project != null)
            //    return String.Equals(Project.OrganizationId, organizationId);

            //if (UserEntity != null)
            //    return UserEntity.OrganizationIds.Contains(organizationId);

            return null;
        }

        public static Project GetProject(this IPrincipal principal) {
            if (principal == null)
                return null;

            //if (Project != null)
            //    return String.Equals(Project.OrganizationId, organizationId);

            //if (UserEntity != null)
            //    return UserEntity.OrganizationIds.Contains(organizationId);

            return null;
        }

        public static bool CanAccessOrganization(this IPrincipal user, string organizationId) {
            if (user == null || String.IsNullOrEmpty(organizationId))
                return false;

            //var ctx = request.GetOwinContext();
            //if (ctx != null && ctx.Request != null && ctx.Request.User != null)
            //    projectId = ctx.Request.User.GetApiKeyProjectId();


            //if (Project != null)
            //    return String.Equals(Project.OrganizationId, organizationId);

            //if (UserEntity != null)
            //    return UserEntity.OrganizationIds.Contains(organizationId);

            return false;
        }

        public static bool IsInOrganization(this IPrincipal user, string organizationId) {
            if (user == null || String.IsNullOrEmpty(organizationId))
                return false;

            //if (Project != null)
            //    return String.Equals(Project.OrganizationId, organizationId);

            //if (UserEntity != null)
            //    return UserEntity.OrganizationIds.Contains(organizationId);

            return false;
        }

        public static IEnumerable<string> GetAssociatedOrganizationIds(this IPrincipal principal) {
            var items = new List<string>();

            //if (UserEntity != null)
            //    items.AddRange(UserEntity.OrganizationIds);
            //else if (Project != null)
            //    items.Add(Project.OrganizationId);

            return items;
        }

        public static string GetDefaultOrganizationId(this IPrincipal principal) {
            if (principal == null)
                return null;

            // TODO: Try to figure out the 1st organization that the user owns instead of just selecting from associated orgs.
            return GetAssociatedOrganizationIds(principal).FirstOrDefault();
        }

        public static string GetAllMessages(this HttpError error, bool includeStackTrace = false) {
            var builder = new StringBuilder();
            HttpError current = error;
            while (current != null) {
                string message = includeStackTrace ? current.FormatMessageWithStackTrace() : current.Message;
                builder.Append(message);

                if (current.ContainsKey("InnerException")) {
                    builder.Append(" --> ");
                    current = current["InnerException"] as HttpError;
                } else
                    current = null;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formats an error with the stack trace included.
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static string FormatMessageWithStackTrace(this HttpError error) {
            if (!error.ContainsKey("ExceptionMessage") || !error.ContainsKey("ExceptionType") || !error.ContainsKey("StackTrace"))
                return error.Message;

            return String.Format("[{0}] {1}\r\nStack Trace:\r\n{2}{3}", error["ExceptionType"], error["ExceptionMessage"], error["StackTrace"], Environment.NewLine);
        }
    }
}