#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Utility;

namespace System.Net.Http {
    public static class HttpClientExtensions {
        public static bool TryGetConfigurationVersion(this HttpResponseMessage response, out int version) {
            version = 0;

            if (response == null)
                return false;

            IEnumerable<string> versions;
            if (!response.Headers.TryGetValues(ExceptionlessHeaders.ConfigurationVersion, out versions))
                return false;

            return int.TryParse(versions.FirstOrDefault(), out version);
        }

        public static void AddBasicAuthentication(this HttpClient client, string user, string password) {
            if (String.IsNullOrEmpty(user))
                throw new ArgumentNullException("user");

            if (String.IsNullOrEmpty(password))
                throw new ArgumentNullException("password");

            string parameter = Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", user, password)));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ExceptionlessHeaders.Token, parameter);
        }

        private const string HttpContext = "MS_HttpContext";
        private const string RemoteEndpointMessage = "System.ServiceModel.Channels.RemoteEndpointMessageProperty";

        public static string GetClientIpAddress(this HttpRequestMessage request) {
            if (request.Properties.ContainsKey(HttpContext)) {
                dynamic ctx = request.Properties[HttpContext];
                if (ctx != null)
                    return ctx.Request.UserHostAddress;
            }

            if (request.Properties.ContainsKey(RemoteEndpointMessage)) {
                dynamic remoteEndpoint = request.Properties[RemoteEndpointMessage];
                if (remoteEndpoint != null)
                    return remoteEndpoint.Address;
            }

            return null;
        }

        #region PATCH Support

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized as JSON.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string requestUri, T value) {
            return client.PatchAsJsonAsync(requestUri, value, CancellationToken.None);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized as JSON. Includes a cancellation
        /// token to cancel the request.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of
        /// cancellation.
        /// </param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string requestUri, T value, CancellationToken cancellationToken) {
            return client.PatchAsync(requestUri, value, new JsonMediaTypeFormatter(), cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized as XML.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsXmlAsync<T>(this HttpClient client, string requestUri, T value) {
            return client.PatchAsXmlAsync(requestUri, value, CancellationToken.None);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized as XML. Includes a cancellation
        /// token to cancel the request.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of
        /// cancellation.
        /// </param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsXmlAsync<T>(this HttpClient client, string requestUri, T value, CancellationToken cancellationToken) {
            return client.PatchAsync(requestUri, value, new XmlMediaTypeFormatter(), cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized using the given formatter.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, string requestUri, T value, MediaTypeFormatter formatter) {
            return client.PatchAsync(requestUri, value, formatter, CancellationToken.None);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized using the given formatter.
        /// Includes a cancellation token to cancel the request.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of
        /// cancellation.
        /// </param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, string requestUri, T value, MediaTypeFormatter formatter, CancellationToken cancellationToken) {
            CancellationToken cancellationToken1 = cancellationToken;
            return client.PatchAsync(requestUri, value, formatter, (MediaTypeHeaderValue)null, cancellationToken1);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized using the given formatter and
        /// media type String.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">
        /// The authoritative value of the Content-Type header. Can be null, in which case the  default
        /// content type of the formatter will be used.
        /// </param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, string requestUri, T value, MediaTypeFormatter formatter, string mediaType) {
            return client.PatchAsync(requestUri, value, formatter, mediaType, CancellationToken.None);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized using the given formatter and
        /// media type String. Includes a cancellation token to cancel the request.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">
        /// The authoritative value of the Content-Type header. Can be null, in which case the  default
        /// content type of the formatter will be used.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of
        /// cancellation.
        /// </param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, string requestUri, T value, MediaTypeFormatter formatter, string mediaType, CancellationToken cancellationToken) {
            MediaTypeHeaderValue mediaTypeHeader = mediaType != null ? new MediaTypeHeaderValue(mediaType) : null;
            return client.PatchAsync(requestUri, value, formatter, mediaTypeHeader, cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request as an asynchronous operation, with a specified value serialized using the given formatter and
        /// media type.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation.
        /// </returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">
        /// The authoritative value of the Content-Type header. Can be null, in which case the  default
        /// content type of the formatter will be used.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of
        /// cancellation.
        /// </param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, string requestUri, T value, MediaTypeFormatter formatter, MediaTypeHeaderValue mediaType, CancellationToken cancellationToken) {
            if (client == null)
                throw new ArgumentNullException("client");

            var method = new HttpMethod("PATCH");
            var content = new ObjectContent<T>(value, formatter, mediaType);
            var request = new HttpRequestMessage(method, requestUri) {
                Content = content
            };

            return client.SendAsync(request, cancellationToken);
        }

        #endregion
    }
}