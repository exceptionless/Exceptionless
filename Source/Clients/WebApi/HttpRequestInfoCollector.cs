#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Web.Http.Controllers;
using Exceptionless.Extensions;
using Exceptionless.Models.Data;

namespace Exceptionless.ExtendedData {
    internal static class RequestInfoCollector {
        public static RequestInfo Collect(HttpActionContext context, IEnumerable<string> exclusions) {
            if (context == null)
                return null;

            var info = new RequestInfo {
                ClientIpAddress = context.Request.GetClientIpAddress(),
                HttpMethod = context.Request.Method.Method
            };

            if (context.Request.Headers.UserAgent != null)
                info.UserAgent = context.Request.Headers.UserAgent.ToString();

            if (context.Request.RequestUri != null) {
                info.Host = context.Request.RequestUri.Host;
                info.IsSecure = context.Request.RequestUri.Scheme == "https";
                info.Path = String.IsNullOrEmpty(context.Request.RequestUri.LocalPath) ? "/" : context.Request.RequestUri.LocalPath;
                info.Port = context.Request.RequestUri.Port;
            }

            if (context.Request.Headers.Referrer != null)
                info.Referrer = context.Request.Headers.Referrer.ToString();

            var exclusionList = exclusions as string[] ?? exclusions.ToArray();
            info.Cookies = context.Request.Headers.GetCookies().ToDictionary(exclusionList);

            //if (context.Request.Form.Count > 0) {
            //    info.PostData = context.Request.Form.AllKeys.Distinct().Where(k => !String.IsNullOrEmpty(k) && !_ignoredFormFields.Contains(k)).ToDictionary(k => k, k => {
            //        try {
            //            return context.Request.Form.Get(k);
            //        } catch (Exception ex) {
            //            return ex.Message;
            //        }
            //    });
            //} else if (context.Request.ContentLength > 0 && context.Request.ContentLength < 1024 * 4) {
            //    try {
            //        context.Request.InputStream.Position = 0;
            //        using (var inputStream = new StreamReader(context.Request.InputStream)) {
            //            info.PostData = inputStream.ReadToEnd();
            //        }
            //    } catch (Exception ex) {
            //        info.PostData = "Error retrieving POST data: " + ex.Message;
            //    }
            //} else if (context.Request.ContentLength > 0) {
            //    string value = Math.Round(context.Request.ContentLength / 1024m, 0).ToString("N0");
            //    info.PostData = String.Format("Data is too large ({0}) to be included.", value + "kb");
            //}

            info.QueryString = context.Request.RequestUri.ParseQueryString().ToDictionary(exclusionList);

            return info;
        }

        private static readonly List<string> _ignoredFormFields = new List<string> {
            "__*"
        };

        private static readonly List<string> _ignoredCookies = new List<string> {
            ".ASPX*",
            "__*",
            "*SessionId*"
        };

        private static Dictionary<string, string> ToDictionary(this IEnumerable<CookieHeaderValue> cookies, IEnumerable<string> exclusions) {
            var d = new Dictionary<string, string>();

            foreach (CookieHeaderValue cookie in cookies) {
                foreach (CookieState innerCookie in cookie.Cookies.Where(k => !String.IsNullOrEmpty(k.Name) && !k.Name.AnyWildcardMatches(_ignoredCookies, true) && !k.Name.AnyWildcardMatches(exclusions, true))) {
                    if (innerCookie != null && !d.ContainsKey(innerCookie.Name))
                        d.Add(innerCookie.Name, innerCookie.Value);
                }
            }

            return d;
        }

        private static Dictionary<string, string> ToDictionary(this NameValueCollection values, IEnumerable<string> exclusions) {
            var d = new Dictionary<string, string>();

            var patternsToMatch = exclusions as string[] ?? exclusions.ToArray();
            foreach (string key in values.AllKeys) {
                if (key.AnyWildcardMatches(_ignoredFormFields, true) || key.AnyWildcardMatches(patternsToMatch, true))
                    continue;

                try {
                    string value = values.Get(key);
                    d.Add(key, value);
                } catch (Exception ex) {
                    if (!d.ContainsKey(key))
                        d.Add(key, ex.Message);
                }
            }

            return d;
        }

        public static string GetClientIpAddress(this HttpRequestMessage request) {
            try {
                if (request.Properties.ContainsKey("MS_HttpContext")) {
                    object context = request.Properties["MS_HttpContext"];
                    if (context != null) {
                        PropertyInfo webRequestProperty = context.GetType().GetProperty("Request");
                        if (webRequestProperty != null) {
                            object webRequest = webRequestProperty.GetValue(context, null);
                            PropertyInfo userHostAddressProperty = webRequestProperty.PropertyType.GetProperty("UserHostAddress");
                            if (userHostAddressProperty != null)
                                return userHostAddressProperty.GetValue(webRequest, null) as string;
                        }
                    }
                }

                if (request.Properties.ContainsKey(RemoteEndpointMessageProperty.Name))
                    return ((RemoteEndpointMessageProperty)request.Properties[RemoteEndpointMessageProperty.Name]).Address;
            } catch {}

            return String.Empty;
        }
    }
}