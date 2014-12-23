// Copyright Exceptionless.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;

namespace Exceptionless.Core.Extensions {
    public static class HttpClientExtensions {
        public static HttpResponseMessage PostAsJson(this HttpClient httpClient, string url, string json) {
            var message = new HttpRequestMessage(HttpMethod.Post, new Uri(url)) {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return httpClient.SendAsync(message).Result;
        }
    }
}