// Copyright Exceptionless.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Extensions {
    public static class HttpClientExtensions {
        public static Task<HttpResponseMessage> PostAsync(this HttpClient httpClient, string url, CancellationToken cancellationToken = default) {
            return httpClient.PostAsync(url, new StringContent(String.Empty), cancellationToken);
        }

        public static Task<HttpResponseMessage> PostAsJsonAsync(this HttpClient httpClient, string url, string json, CancellationToken cancellationToken = default) {
            var message = new HttpRequestMessage(HttpMethod.Post, new Uri(url)) {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return httpClient.SendAsync(message, cancellationToken);
        }
    }
}