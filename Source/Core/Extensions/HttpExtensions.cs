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
using System.Security.Claims;
using System.ServiceModel.Channels;
using System.Text;
using System.Web.Http;
using Exceptionless.Core.Authorization;
using Exceptionless.Models;

namespace Exceptionless.Core.Extensions {
    public static class HttpExtensions {
        public static User GetUser(this HttpRequestMessage message) {
            if (message == null)
                return null;

            var user = message.GetOwinContext().Get<Lazy<User>>("LazyUser");
            if (user != null)
                return user.Value;

            return null;
        }

        public static Project GetProject(this HttpRequestMessage message) {
            if (message == null)
                return null;

            var project = message.GetOwinContext().Get<Lazy<Project>>("LazyProject");
            if (project != null)
                return project.Value;

            return null;
        }

        public static ClaimsPrincipal GetClaimsPrincipal(this HttpRequestMessage message) {
            var context = message.GetOwinContext();
            if (context == null || context.Request == null || context.Request.User == null)
                return null;

            return context.Request.User.GetClaimsPrincipal();
        }

        public static AuthType GetAuthType(this HttpRequestMessage message) {
            var principal = message.GetClaimsPrincipal();
            return principal == null ? AuthType.Anonymous : principal.GetAuthType();
        }

        public static bool CanAccessOrganization(this HttpRequestMessage message, string organizationId) {
            if (message.IsInOrganization(organizationId))
                return true;

            var principal = message.GetClaimsPrincipal();
            return principal != null && principal.IsInRole(AuthorizationRoles.GlobalAdmin);
        }

        public static bool IsInOrganization(this HttpRequestMessage message, string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return false;

            var authType = message.GetAuthType();
            if (authType == AuthType.User)
                return message.GetUser().OrganizationIds.Contains(organizationId);

            if (authType == AuthType.Project)
                return message.GetProject().OrganizationId == organizationId;

            return false;
        }

        public static IEnumerable<string> GetAssociatedOrganizationIds(this HttpRequestMessage message) {
            var items = new List<string>();

            var authType = message.GetAuthType();
            if (authType == AuthType.User)
                items.AddRange(message.GetUser().OrganizationIds);

            if (authType == AuthType.Project)
                items.Add(message.GetProject().OrganizationId);

            return items;
        }

        public static string GetDefaultOrganizationId(this HttpRequestMessage message) {
            // TODO: Try to figure out the 1st organization that the user owns instead of just selecting from associated orgs.
            return message.GetAssociatedOrganizationIds().FirstOrDefault();
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