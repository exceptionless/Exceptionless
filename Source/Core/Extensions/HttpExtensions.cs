#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net.Http;
using System.Text;
using System.Web.Http;
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