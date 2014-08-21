#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Web;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models.Data;

namespace Exceptionless.ExtendedData {
    internal static class RequestInfoCollector {
        private const int MAX_DATA_ITEM_LENGTH = 1000;

        public static RequestInfo Collect(HttpContextBase context, ExceptionlessConfiguration config) {
            if (context == null)
                return null;

            var info = new RequestInfo {
                HttpMethod = context.Request.HttpMethod,
                UserAgent = context.Request.UserAgent,
                Path = String.IsNullOrEmpty(context.Request.Path) ? "/" : context.Request.Path
            };

            try {
                info.ClientIpAddress = context.Request.UserHostAddress;
            } catch (ArgumentException ex) {
                config.Resolver.GetLog().Error(ex, "An error occurred while setting the Client Ip Address.");
            }

            try {
                info.IsSecure = context.Request.IsSecureConnection;
            } catch (ArgumentException ex) {
                config.Resolver.GetLog().Error(ex, "An error occurred while setting Is Secure Connection.");
            }

            if (context.Request.Url != null)
                info.Host = context.Request.Url.Host;

            if (context.Request.UrlReferrer != null)
                info.Referrer = context.Request.UrlReferrer.ToString();

            if (context.Request.Url != null)
                info.Port = context.Request.Url.Port;

            info.Cookies = context.Request.Cookies.ToDictionary(config.DataExclusions);

            if (context.Request.Form.Count > 0)
                info.PostData = context.Request.Form.ToDictionary(config.DataExclusions);
            else if (context.Request.ContentLength > 0 && context.Request.ContentLength < 1024 * 4) {
                try {
                    context.Request.InputStream.Position = 0;
                    using (var inputStream = new StreamReader(context.Request.InputStream))
                        info.PostData = inputStream.ReadToEnd();
                } catch (Exception ex) {
                    info.PostData = "Error retrieving POST data: " + ex.Message;
                }
            } else if (context.Request.ContentLength > 0) {
                string value = Math.Round(context.Request.ContentLength / 1024m, 0).ToString("N0");
                info.PostData = String.Format("Data is too large ({0}) to be included.", value + "kb");
            }

            try {
                info.QueryString = context.Request.QueryString.ToDictionary(config.DataExclusions);
            } catch (Exception ex) {
                config.Resolver.GetLog().Error(ex, "An error occurred while getting the cookies");
            }

            return info;
        }

        private static readonly List<string> _ignoredFormFields = new List<string> {
            "*VIEWSTATE*",
            "*EVENTVALIDATION*"
        };

        private static readonly List<string> _ignoredCookies = new List<string> {
            "*ASPX*",
            "__RequestVerificationToken",
            "ASP.NET_SessionId",
            "__LastErrorId"
        };

        private static Dictionary<string, string> ToDictionary(this HttpCookieCollection cookies, IEnumerable<string> exclusions) {
            var d = new Dictionary<string, string>();

            foreach (string key in cookies.AllKeys.Distinct().Where(k => !String.IsNullOrEmpty(k) && !k.AnyWildcardMatches(_ignoredCookies, true) && !k.AnyWildcardMatches(exclusions, true))) {
                try {
                    HttpCookie cookie = cookies.Get(key);
                    if (cookie != null && cookie.Value != null && cookie.Value.Length < MAX_DATA_ITEM_LENGTH && !d.ContainsKey(key))
                        d.Add(key, cookie.Value);
                } catch (Exception ex) {
                    if (!d.ContainsKey(key))
                        d.Add(key, ex.Message);
                }
            }

            return d;
        }

        private static Dictionary<string, string> ToDictionary(this NameValueCollection values, IEnumerable<string> exclusions) {
            var d = new Dictionary<string, string>();

            var exclusionsArray = exclusions as string[] ?? exclusions.ToArray();
            foreach (string key in values.AllKeys) {
                if (key.AnyWildcardMatches(_ignoredFormFields, true) || key.AnyWildcardMatches(exclusionsArray, true))
                    continue;

                try {
                    string value = values.Get(key);
                    if (value != null && !d.ContainsKey(key) && value.Length < MAX_DATA_ITEM_LENGTH)
                        d.Add(key, value);
                } catch (Exception ex) {
                    if (!d.ContainsKey(key))
                        d.Add(key, "EXCEPTION: " + ex.Message);
                }
            }

            return d;
        }
    }
}