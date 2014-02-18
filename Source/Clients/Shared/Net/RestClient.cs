#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Exceptionless.Extensions;
using Exceptionless.Json;
using Exceptionless.Serialization;

namespace Exceptionless.Net {
    internal class RestClient {
        private const string DEFAULT_CONTENT_TYPE = "application/json";
        private const string HTTP_METHOD_OVERRIDE = "X-HTTP-Method-Override";
        private readonly ManualResetEvent _complete = new ManualResetEvent(false);

        static RestClient() {
            // ignore invalid SSL certificate warnings.
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public RestClient(Uri baseUri) {
            BaseUri = baseUri;
            RequestHeaders = new WebHeaderCollection();
            ResponseHeaders = new WebHeaderCollection();
            RequestContentType = DEFAULT_CONTENT_TYPE;
            ResponseContentType = DEFAULT_CONTENT_TYPE;
            Timeout = TimeSpan.FromMinutes(1);
        }

        #region Events

        public event EventHandler<RestRequestCompletedEventArgs> RestRequestCompleted;

        protected void OnRestRequestComplete(RestState state) {
            EventHandler<RestRequestCompletedEventArgs> handle = RestRequestCompleted;
            if (handle == null)
                return;

            var e = new RestRequestCompletedEventArgs(
                state.Error,
                state.IsCancelled,
                state.UserToken,
                state);

            handle.Invoke(this, e);
        }

        #endregion

        #region Properties

        public AuthorizationHeader AuthorizationHeader { get; set; }
        public Action<AuthorizationState> AuthorizationCallback { get; set; }

        public Uri BaseUri { get; set; }
        public string RequestContentType { get; set; }
        public string ResponseContentType { get; set; }
        public WebHeaderCollection RequestHeaders { get; private set; }
        public WebHeaderCollection ResponseHeaders { get; private set; }
        public ICredentials Credentials { get; set; }
        public Exception Error { get; private set; }
        public string Status { get; private set; }
        public int? StatusCode { get; private set; }
        public TimeSpan Timeout { get; set; }
        public bool UseMethodOverride { get; set; }
#if !SILVERLIGHT
        public IWebProxy Proxy { get; set; }
#endif

        #endregion

        #region Send

        public TResponse Post<TRequest, TResponse>(string endPoint, TRequest data) where TResponse : class {
            return Post<TRequest, TResponse>(new Uri(endPoint, UriKind.Relative), data);
        }

        public TResponse Post<TRequest, TResponse>(Uri endPoint, TRequest data) where TResponse : class {
            return Send<TRequest, TResponse>(endPoint, data, "POST");
        }

        public TResponse Put<TRequest, TResponse>(string endPoint, TRequest data) where TResponse : class {
            return Put<TRequest, TResponse>(new Uri(endPoint, UriKind.Relative), data);
        }

        public TResponse Put<TRequest, TResponse>(Uri endPoint, TRequest data) where TResponse : class {
            return Send<TRequest, TResponse>(endPoint, data, "PUT");
        }

        public TResponse Patch<TRequest, TResponse>(string endPoint, TRequest data) where TResponse : class {
            return Patch<TRequest, TResponse>(new Uri(endPoint, UriKind.Relative), data);
        }

        public TResponse Patch<TRequest, TResponse>(Uri endPoint, TRequest data) where TResponse : class {
            return Send<TRequest, TResponse>(endPoint, data, "PATCH");
        }

        public TResponse Get<TResponse>(string endPoint) where TResponse : class {
            return Get<TResponse>(new Uri(endPoint, UriKind.Relative));
        }

        public TResponse Get<TResponse>(Uri endPoint) where TResponse : class {
            return Send<object, TResponse>(endPoint, null, "GET");
        }

        public TResponse Send<TRequest, TResponse>(Uri endPoint, TRequest data, string method) where TResponse : class {
#if SILVERLIGHT
            if (System.Windows.Deployment.Current.Dispatcher.CheckAccess())
                throw new InvalidOperationException("Invoking this method on the UI thread is forbidden.");
#endif
            RestState state = null;

            // loop for trying authentication
            for (int i = 0; i < 2; i++) {
                state = CreateState<TRequest, TResponse>(endPoint, data, method);
                Send(state);

                // wait for async requests
                _complete.WaitOne();

                // if request was unauthorized
                if (StatusCode != 401 || AuthorizationCallback == null)
                    break;

                // try to authenticate
                var authorizationState = new AuthorizationState();
                authorizationState.IsRefresh = true;

                AuthorizationCallback(authorizationState);
                AuthorizationHeader = authorizationState.Header;

                if (authorizationState.IsAuthenticated)
                    continue;

                if (authorizationState.Error != null)
                    Error = authorizationState.Error;

                break;
            }

            if (state == null)
                return default(TResponse);

            if (typeof(HttpWebResponse) == typeof(TResponse))
                return state.Response as TResponse;

            if (!state.Response.IsSuccessStatusCode())
                return default(TResponse);

            return state.ResponseData == null ? default(TResponse) : (TResponse)state.ResponseData;
        }

        #endregion

        #region SendAsync

        public void PostAsync<TRequest, TResponse>(string endPoint, TRequest data) {
            PostAsync<TRequest, TResponse>(endPoint, data, null);
        }

        public void PostAsync<TRequest, TResponse>(string endPoint, TRequest data, object userToken) {
            PostAsync<TRequest, TResponse>(new Uri(endPoint, UriKind.Relative), data, userToken);
        }

        public void PostAsync<TRequest, TResponse>(Uri endPoint, TRequest data, object userToken) {
            SendAsync<TRequest, TResponse>(endPoint, data, "POST", userToken);
        }

        public void PutAsync<TRequest, TResponse>(string endPoint, TRequest data) {
            PutAsync<TRequest, TResponse>(endPoint, data, null);
        }

        public void PutAsync<TRequest, TResponse>(string endPoint, TRequest data, object userToken) {
            PutAsync<TRequest, TResponse>(new Uri(endPoint, UriKind.Relative), data, userToken);
        }

        public void PutAsync<TRequest, TResponse>(Uri endPoint, TRequest data, object userToken) {
            SendAsync<TRequest, TResponse>(endPoint, data, "PUT", userToken);
        }

        public void PatchAsync<TRequest, TResponse>(string endPoint, TRequest data) {
            PatchAsync<TRequest, TResponse>(endPoint, data, null);
        }

        public void PatchAsync<TRequest, TResponse>(string endPoint, TRequest data, object userToken) {
            PatchAsync<TRequest, TResponse>(new Uri(endPoint, UriKind.Relative), data, userToken);
        }

        public void PatchAsync<TRequest, TResponse>(Uri endPoint, TRequest data, object userToken) {
            SendAsync<TRequest, TResponse>(endPoint, data, "PATCH", userToken);
        }

        public void GetAsync<TResponse>(string endPoint) {
            GetAsync<TResponse>(endPoint, null);
        }

        public void GetAsync<TResponse>(string endPoint, object userToken) {
            GetAsync<TResponse>(new Uri(endPoint, UriKind.Relative), userToken);
        }

        public void GetAsync<TResponse>(Uri endPoint, object userToken) {
            SendAsync<object, TResponse>(endPoint, null, "GET", userToken);
        }

        public void SendAsync<TRequest, TResponse>(Uri endPoint, TRequest data, string method, object userToken) {
            RestState state = CreateState<TRequest, TResponse>(endPoint, data, method);
            state.UserToken = userToken;

            Send(state);
        }

        #endregion

        public bool HasError() {
            return Error != null;
        }

        private RestState CreateState<TRequest, TResponse>(Uri endPoint, TRequest data, string method) {
            Reset();

            var state = new RestState();
            state.EndPoint = new Uri(BaseUri, endPoint);

            state.RequestData = data;
            state.RequestType = typeof(TRequest);
            state.ResponseType = typeof(TResponse);
            state.Method = method;

            return state;
        }

        private void Send(RestState state) {
#if SILVERLIGHT
            var webRequest = (HttpWebRequest)System.Net.Browser.WebRequestCreator.ClientHttp.Create(state.EndPoint);
            if (!state.IsGet())
                webRequest.ContentType = RequestContentType;
#else
            var webRequest = (HttpWebRequest)WebRequest.Create(state.EndPoint);
            webRequest.AllowAutoRedirect = true;
            webRequest.ContentType = RequestContentType;
            webRequest.Accept = ResponseContentType;
            webRequest.UserAgent = "Exceptionless/" + ThisAssembly.AssemblyFileVersion;
            webRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.None;
#endif
            webRequest.Method = state.Method;
            if (!state.IsGet())
                webRequest.Headers["Content-Encoding"] = "gzip";

            if (AuthorizationHeader != null)
                webRequest.Headers[HttpRequestHeader.Authorization] = AuthorizationHeader.ToString();

            if (!state.IsGet() && !state.IsPost() && UseMethodOverride) {
                webRequest.Method = "POST";
                webRequest.Headers[HTTP_METHOD_OVERRIDE] = state.Method;
            }

            CopyHeaders(RequestHeaders, webRequest.Headers);

            webRequest.UseDefaultCredentials = true;
            if (Credentials != null)
                webRequest.Credentials = Credentials;
#if !SILVERLIGHT
            if (Proxy != null)
                webRequest.Proxy = Proxy;
#endif
            state.Request = webRequest;

            if (state.IsGet())
                BeginGetResponse(state);
            else
                webRequest.BeginGetRequestStream(OnRequestStream, state);
        }

        private void Reset() {
#if SILVERLIGHT
            ResponseHeaders = new WebHeaderCollection();
#else
            ResponseHeaders.Clear();
#endif
            Error = null;
            Status = null;
            StatusCode = null;
            _complete.Reset();
        }

        private void BeginGetResponse(RestState state) {
            IAsyncResult handle = state.Request.BeginGetResponse(OnResponse, state);
#if !SILVERLIGHT
            // create async timeout
            ThreadPool.RegisterWaitForSingleObject(
                                                   handle.AsyncWaitHandle,
                OnTimeout,
                state,
                Timeout,
                true);
#endif
        }

        private void CompleteProcess(RestState state) {
            try {
                HttpWebResponse webResponse = state.Response;
                if (webResponse != null) {
#if SILVERLIGHT
                    if (webResponse.SupportsHeaders)
                        CopyHeaders(webResponse.Headers, ResponseHeaders);
#else
                    CopyHeaders(webResponse.Headers, ResponseHeaders);
#endif
                    StatusCode = (int)webResponse.StatusCode;
                    Status = String.Format("{0} {1}", StatusCode, webResponse.StatusDescription);
                }

                if (state.Error == null)
                    return;

                Error = state.Error;
                var webEx = Error as WebException;
                if (webEx != null && String.IsNullOrEmpty(Status))
                    Status = webEx.Status.ToString().ToSpacedWords();
            } catch (Exception ex) {
                Error = ex;
            } finally {
                OnRestRequestComplete(state);
                _complete.Set();
            }
        }

        private static void OnTimeout(object state, bool timedOut) {
            // Abort the request if the timer fires.
            if (!timedOut)
                return;

            var requestState = state as RestState;
            if (requestState == null)
                return;

            if (requestState.Request == null)
                return;

            requestState.Request.Abort();
            requestState.IsCancelled = true;
        }

        private void OnRequestStream(IAsyncResult ar) {
            var state = (RestState)ar.AsyncState;
            try {
                using (Stream requestStream = state.Request.EndGetRequestStream(ar)) {
                    using (var zipStream = new GZipStream(requestStream, CompressionMode.Compress)) {
                        if (state.RequestType == typeof(string))
                            WriteRequestString(zipStream, (string)state.RequestData);
                        else if (state.RequestType == typeof(byte[]))
                            WriteRequestBytes(zipStream, (byte[])state.RequestData);
                        else
                            WriteRequestType(zipStream, state.RequestData);

                        zipStream.Close();
                        requestStream.Close();
                    }
                }

                BeginGetResponse(state);
            } catch (Exception ex) {
                state.Error = ex;
                CompleteProcess(state);
            }
        }

        private void WriteRequestString(Stream stream, string data) {
#if SILVERLIGHT
            byte[] bytes = Encoding.UTF8.GetBytes(data);
#else
            byte[] bytes = Encoding.Default.GetBytes(data);
#endif
            WriteRequestBytes(stream, bytes);
        }

        private void WriteRequestBytes(Stream stream, byte[] data) {
            stream.Write(data, 0, data.Length);
        }

        private void WriteRequestType(Stream stream, object data) {
            var serializer = new JsonSerializer();
            using (var sw = new StreamWriter(stream)) {
                using (var jw = new JsonTextWriter(sw))
                    serializer.Serialize(jw, data);
            }
        }

        private void OnResponse(IAsyncResult ar) {
            var state = (RestState)ar.AsyncState;
            HttpWebResponse webResponse = null;

            try {
                WebRequest webRequest = state.Request;

                try {
                    webResponse = (HttpWebResponse)webRequest.EndGetResponse(ar);
                } catch (WebException wex) {
                    state.Error = wex;
                    webResponse = wex.Response as HttpWebResponse;
                }

                state.Response = webResponse;

                if (webResponse == null || state.ResponseType == typeof(HttpWebResponse))
                    return;

                using (Stream responseStream = webResponse.GetResponseStream()) {
                    if (state.ResponseType == typeof(string))
                        state.ResponseData = ReadResponseString(responseStream, state);
                    else if (state.ResponseType == typeof(byte[]))
                        state.ResponseData = ReadResponseBytes(responseStream);
                    else
                        state.ResponseData = ReadResponseType(responseStream, state);
                }
            } catch (Exception ex) {
                state.Error = ex;
            } finally {
                if (webResponse != null)
                    webResponse.Close();

                // copy response to local properties
                CompleteProcess(state);
            }
        }

        private string ReadResponseString(Stream stream, RestState state) {
            byte[] data = ReadResponseBytes(stream);
            return GetStringUsingEncoding(state.Request, data);
        }

        private byte[] ReadResponseBytes(Stream stream) {
            byte[] data;

            using (var ms = new MemoryStream()) {
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, bytesRead);

                data = ms.ToArray();
            }

            return data;
        }

        private object ReadResponseType(Stream stream, RestState state) {
            if (stream == null || state.Response == null)
                return null;

            string contentType = new ContentTypeHeader(state.Response.ContentType).ContentType ?? DEFAULT_CONTENT_TYPE;
            if (!contentType.Equals(DEFAULT_CONTENT_TYPE))
                return null;

            return ModelSerializer.Current.Deserialize(stream, state.ResponseType);
        }

        private bool ByteArrayHasPrefix(byte[] prefix, byte[] byteArray) {
            if (prefix == null || byteArray == null || prefix.Length > byteArray.Length)
                return false;
            for (int i = 0; i < prefix.Length; i++) {
                if (prefix[i] != byteArray[i])
                    return false;
            }
            return true;
        }

        // taken from WebClient source code
        private string GetStringUsingEncoding(WebRequest request, byte[] data) {
            Encoding enc = null;
            int bomLengthInData = -1;

            // Figure out encoding by first checking for encoding string in Content-Type HTTP header
            // This can throw NotImplementedException if the derived class of WebRequest doesn't support it. 
            string contentType;
            try {
                contentType = request.ContentType;
            } catch (NotImplementedException) {
                contentType = null;
            } catch (NotSupportedException) // need this since our FtpWebRequest class mistakenly does this
            {
                contentType = null;
            }
            // Unexpected exceptions are thrown back to caller

            if (contentType != null) {
                contentType = contentType.ToLower(CultureInfo.InvariantCulture);
                string[] parsedList = contentType.Split(new char[] { ';', '=', ' ' });
                bool nextItem = false;
                foreach (string item in parsedList) {
                    if (item == "charset")
                        nextItem = true;
                    else if (nextItem) {
                        try {
                            enc = Encoding.GetEncoding(item);
                        } catch (ArgumentException) {
                            // Eat ArgumentException here.
                            // We'll assume that Content-Type encoding might have been garbled and wasn't present at all. 
                            break;
                        }
                        // Unexpected exceptions are thrown back to caller
                    }
                }
            }

            // If no content encoding listed in the ContentType HTTP header, or no Content-Type header present, then
            // check for a byte-order-mark (BOM) in the data to figure out encoding. 
            if (enc == null) {
                byte[] preamble;
                Encoding[] encodings = { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode };
                for (int i = 0; i < encodings.Length; i++) {
                    preamble = encodings[i].GetPreamble();
                    if (ByteArrayHasPrefix(preamble, data)) {
                        enc = encodings[i];
                        bomLengthInData = preamble.Length;
                        break;
                    }
                }
            }

            // Do we have an encoding guess?  If not, use default. 
            if (enc == null)
#if SILVERLIGHT
                enc = Encoding.UTF8;
#else
                enc = Encoding.Default;
#endif

            // Calculate BOM length based on encoding guess.  Then check for it in the data. 
            if (bomLengthInData == -1) {
                byte[] preamble = enc.GetPreamble();
                if (ByteArrayHasPrefix(preamble, data))
                    bomLengthInData = preamble.Length;
                else
                    bomLengthInData = 0;
            }

            // Convert byte array to string stripping off any BOM before calling GetString().
            // This is required since GetString() doesn't handle stripping off BOM. 
            return enc.GetString(data, bomLengthInData, data.Length - bomLengthInData);
        }

        private static void CopyHeaders(WebHeaderCollection source, WebHeaderCollection target) {
            foreach (string key in source.AllKeys)
                target[key] = source[key];
        }
    }
}