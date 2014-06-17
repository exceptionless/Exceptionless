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
using Exceptionless.Extensions;
using Exceptionless.Models.Data;
using Nancy;
using Nancy.Helpers;

namespace Exceptionless.ExtendedData {
    internal static class NancyRequestInfoCollector {
        public static RequestInfo Collect(NancyContext context, ICollection<string> exclusions) {
            if (context == null)
                return null;

            var info = new RequestInfo {
                ClientIpAddress = context.Request.UserHostAddress,
                HttpMethod = context.Request.Method
            };

            if (context.Request.Headers.UserAgent != null)
                info.UserAgent = context.Request.Headers.UserAgent;

            if (context.Request.Url != null) {
                info.Host = context.Request.Url.HostName;
                info.IsSecure = context.Request.Url.IsSecure;
                info.Path = context.Request.Url.BasePath + context.Request.Url.Path;
                info.Port = context.Request.Url.Port ?? 80;
            }

            if (context.Request.Headers.Referrer != null)
                info.Referrer = context.Request.Headers.Referrer;

            info.Cookies = context.Request.Cookies.ToDictionary(exclusions);

            if (context.Request.Url != null && !String.IsNullOrWhiteSpace(context.Request.Url.Query))
                info.QueryString = HttpUtility.ParseQueryString(context.Request.Url.Query).ToDictionary(exclusions);

            return info;
        }

        private static readonly List<string> _ignoredFormFields = new List<string> {
            "__*"
        };

        private static readonly List<string> _ignoredCookies = new List<string> {
            ".ASPX*",
            "__*",
            "*SessionId*",
            "_ncfa"
        };

        private static Dictionary<string, string> ToDictionary(this IEnumerable<KeyValuePair<string, string>> cookies, ICollection<string> exclusions) {
            var d = new Dictionary<string, string>();

            foreach (var kv in cookies.Where(pair => !String.IsNullOrEmpty(pair.Key) && !pair.Key.AnyWildcardMatches(_ignoredCookies, true) && !pair.Key.AnyWildcardMatches(exclusions, true)))
            {
                if (!d.ContainsKey(kv.Key))
                    d.Add(kv.Key, kv.Value);
            }

            return d;
        }

        private static Dictionary<string, string> ToDictionary(this NameValueCollection values, ICollection<string> exclusions) {
            var d = new Dictionary<string, string>();

            foreach (string key in values.AllKeys) {
                if (key.AnyWildcardMatches(_ignoredFormFields, true) || key.AnyWildcardMatches(exclusions, true))
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
    }
}