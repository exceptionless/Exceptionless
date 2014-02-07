#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Net;
using Exceptionless.Net;

namespace Exceptionless.Extensions {
    internal static class HttpWebResponseExtensions {
        public static bool IsSuccessStatusCode(this HttpWebResponse response) {
            if (response != null && response.StatusCode >= HttpStatusCode.OK)
                return response.StatusCode <= (HttpStatusCode)299;

            return false;
        }

        public static bool TryParseCreatedUri(this HttpWebResponse response, out string id) {
            id = null;

            if (!response.IsSuccessStatusCode() || response.StatusCode != HttpStatusCode.Created)
                return false;

            string value = response.Headers[HttpResponseHeader.Location];
            if (String.IsNullOrEmpty(value))
                return false;

            var uri = new Uri(value);
            if (uri.Segments.Length == 0)
                return false;

            id = uri.Segments[uri.Segments.Length - 1];

            return true;
        }

        public static bool ShouldUpdateConfiguration(this HttpWebResponse response, int currentVersion) {
            if (response == null)
                return false;

            string value = response.Headers.Get(ExceptionlessHeaders.ConfigurationVersion);

            int v;
            if (String.IsNullOrEmpty(value) || !int.TryParse(value, out v))
                return false;

            return currentVersion < v;
        }
    }
}