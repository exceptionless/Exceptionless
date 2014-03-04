#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Submission;
using Exceptionless.Submission.Net;

namespace Exceptionless.Extensions {
    public static class WebRequestExtensions {
        public const string JSON_CONTENT_TYPE = "application/json";

        public static Task<Stream> GetRequestStreamAsync(this WebRequest request) {
            return Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null);
        }

        public static Task<WebResponse> GetResponseAsync(this WebRequest request) {
            return Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null);
        }

        public static void AddAuthorizationHeader(this WebRequest request, Configuration configuration) {
            var authorizationHeader = new AuthorizationHeader {
                Scheme = "Basic",
                ParameterText = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", "client", configuration.ApiKey)))
            };

            request.Headers[HttpRequestHeader.Authorization] = authorizationHeader.ToString();
        }

        public static Task<WebResponse> PostJsonAsync(this HttpWebRequest request, string data) {
            request.Accept = request.ContentType = JSON_CONTENT_TYPE;
            request.Method = "POST";

            byte[] buffer = Encoding.UTF8.GetBytes(data);
            return request.GetRequestStreamAsync()
                .Success(t => t.Result.Write(buffer, 0, buffer.Length))
                .Success(t => request.GetResponseAsync()).Unwrap();
        }

        public static Task<WebResponse> GetJsonAsync(this HttpWebRequest request) {
            request.Accept = JSON_CONTENT_TYPE;
            request.Method = "GET";

            return request.GetResponseAsync();
        }
    }
}