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
using System.Reflection;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Nancy;
using Nancy.Extensions;
using Nancy.Helpers;

namespace Exceptionless.ExtendedData
{
    internal static class NancyRequestInfoCollector
    {
        public static RequestInfo Collect(NancyContext context, ExceptionlessClient client)
        {
            if (context == null)
                return null;
            var request = context.Request;
            var info = new RequestInfo
            {
                ClientIpAddress = request.UserHostAddress.Equals("::1") ? "localhost" : request.UserHostAddress,
                HttpMethod = request.Method
            };

            if (request.Headers.UserAgent != null)
                info.UserAgent = request.Headers.UserAgent;

            if (request.Url != null)
            {
                info.Host = request.Url.HostName;
                info.IsSecure = request.Url.IsSecure;
                info.Path = request.Url.BasePath + request.Url.Path;
                info.Port = request.Url.Port ?? 80;
            }

            if (request.Headers.Referrer != null)
                info.Referrer = request.Headers.Referrer;

            info.Cookies = request.Cookies.ToDictionary(client);

            info.QueryString = HttpUtility.ParseQueryString(request.Url.Query).ToDictionary(client);

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

        private static Dictionary<string, string> ToDictionary(this IDictionary<string, string> kvs, ExceptionlessClient client)
        {
            var d = new Dictionary<string, string>();
            foreach (var kv in kvs)
            {
                if (kv.Key.AnyWildcardMatches(_ignoredFormFields, true)
                    || kv.Key.AnyWildcardMatches(_ignoredCookies, true)
                    || kv.Key.AnyWildcardMatches(client.Configuration.DataExclusions, true))
                {
                    continue;
                }
                d.Add(kv.Key, kv.Value);
            }
            return d;
        }
        public static Dictionary<string, string> ToDictionary(this NameValueCollection collection, ExceptionlessClient client)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (var key in collection.AllKeys)
            {
                dictionary.Add(key, collection[key]);
            }
            return dictionary.ToDictionary(client);
        }
    }
}