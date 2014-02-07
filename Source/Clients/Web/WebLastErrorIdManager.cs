#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Web;
using Exceptionless.Logging;

namespace Exceptionless {
    internal class WebLastErrorIdManager : ILastErrorIdManager {
        private const string LAST_ERROR_ID_KEY = "__LastErrorId";

        public WebLastErrorIdManager(IExceptionlessLogAccessor logAccessor) {
            if (logAccessor == null)
                throw new ArgumentNullException("logAccessor");

            LogAccessor = logAccessor;
        }

        /// <summary>
        /// The log accessor used for diagnostic information.
        /// </summary>
        public IExceptionlessLogAccessor LogAccessor { get; set; }

        /// <summary>
        /// Gets the last error id that was submitted to the server.
        /// </summary>
        /// <returns>The error id</returns>
        public string GetLast() {
            try {
                HttpContext httpContext = HttpContext.Current;
                if (httpContext == null)
                    throw new InvalidOperationException("WebLastErrorIdManager can only be used in web contexts.");

                if (httpContext.Session != null && httpContext.Session[LAST_ERROR_ID_KEY] != null)
                    return httpContext.Session[LAST_ERROR_ID_KEY].ToString();

                if (httpContext.Request.Cookies[LAST_ERROR_ID_KEY] != null)
                    return httpContext.Request.Cookies[LAST_ERROR_ID_KEY].Value;
            } catch (Exception e) {
                LogAccessor.Log.Warn("Error getting last error id: {0}", e.Message);
            }

            return null;
        }

        /// <summary>
        /// Clears the last error id.
        /// </summary>
        public void ClearLast() {
            HttpContext httpContext = HttpContext.Current;
            if (httpContext == null)
                return;

            if (httpContext.Session != null)
                httpContext.Session.Remove(LAST_ERROR_ID_KEY);

            if (httpContext.Request.Cookies[LAST_ERROR_ID_KEY] == null)
                return;

            HttpCookie cookie = httpContext.Request.Cookies[LAST_ERROR_ID_KEY];
            if (cookie == null)
                return;

            cookie.Expires = DateTime.UtcNow.AddDays(-1);
            httpContext.Response.Cookies.Add(cookie);
        }

        public void SetLast(string errorId) {
            HttpContext httpContext = HttpContext.Current;
            if (httpContext == null)
                return;

            if (httpContext.Session != null)
                httpContext.Session[LAST_ERROR_ID_KEY] = errorId;

            // Session doesn't seem to be reliable so set it in a cookie as well.
            try {
                var cookie = new HttpCookie(LAST_ERROR_ID_KEY);
                cookie.HttpOnly = true;
                cookie.Value = errorId;
                httpContext.Response.Cookies.Add(cookie);
            } catch (Exception e) {
                LogAccessor.Log.Warn("Error setting error id cookie: {0}", e.Message);
            }
        }
    }
}