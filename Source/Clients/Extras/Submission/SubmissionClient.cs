#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Exceptionless.Configuration;
using Exceptionless.Extensions;
using Exceptionless.Extras.Extensions;
using Exceptionless.Json.Linq;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Submission;
using Exceptionless.Submission.Net;

namespace Exceptionless.Extras.Submission {
    public class SubmissionClient : ISubmissionClient {
        static SubmissionClient() {
            ConfigureServicePointManagerSettings();
        }

        public SubmissionResponse PostEvents(IEnumerable<Event> events, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            var data = serializer.Serialize(events);

            HttpWebResponse response;
            try {
                var request = CreateHttpWebRequest(config, "events");
                response = request.PostJsonAsyncWithCompression(data).Result as HttpWebResponse;
            } catch (AggregateException aex) {
                var ex = aex.GetInnermostException() as WebException;
                if (ex != null)
                    response = (HttpWebResponse)ex.Response;
                else
                    return new SubmissionResponse(500, message: aex.GetMessage());
            } catch (Exception ex) {
                return new SubmissionResponse(500, message: ex.Message);
            }

            int settingsVersion;
            if (Int32.TryParse(response.Headers[ExceptionlessHeaders.ConfigurationVersion], out settingsVersion))
                SettingsManager.CheckVersion(settingsVersion, config);

            return new SubmissionResponse((int)response.StatusCode, GetResponseMessage(response));
        }

        public SubmissionResponse PostUserDescription(string referenceId, UserDescription description, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            var data = serializer.Serialize(description);

            HttpWebResponse response;
            try {
                var request = CreateHttpWebRequest(config, String.Format("events/by-ref/{0}/user-description", referenceId));
                response = request.PostJsonAsyncWithCompression(data).Result as HttpWebResponse;
            } catch (AggregateException aex) {
                var ex = aex.GetInnermostException() as WebException;
                if (ex != null)
                    response = (HttpWebResponse)ex.Response;
                else
                    return new SubmissionResponse(500, message: aex.GetMessage());
            } catch (Exception ex) {
                return new SubmissionResponse(500, message: ex.Message);
            }

            return new SubmissionResponse((int)response.StatusCode, GetResponseMessage(response));
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration config, IJsonSerializer serializer) {
            HttpWebResponse response;
            try {
                var request = CreateHttpWebRequest(config, "projects/config");
                response = request.GetJsonAsync().Result as HttpWebResponse;
            } catch (Exception ex) {
                var message = String.Concat("Unable to retrieve configuration settings. Exception: ", ex.GetMessage());
                return new SettingsResponse(false, message: message);
            }

            if (response == null || response.StatusCode != HttpStatusCode.OK)
                return new SettingsResponse(false, message: String.Format("Unable to retrieve configuration settings: {0}", GetResponseMessage(response)));

            var json = response.GetResponseText();
            if (String.IsNullOrWhiteSpace(json))
                return new SettingsResponse(false, message: "Invalid configuration settings.");

            var settings = serializer.Deserialize<ClientConfiguration>(json);
            return new SettingsResponse(true, settings.Settings, settings.Version);
        }

        private static string GetResponseMessage(HttpWebResponse response) {
            if (response.IsSuccessful())
                return null;

            int statusCode = (int)response.StatusCode;
            string responseText = response.GetResponseText();
            string message = statusCode == 404 ? "404 Page not found." : responseText.Length < 500 ? responseText : "";

            if (responseText.Trim().StartsWith("{")) {
                try {
                    var responseJson = JObject.Parse(responseText);
                    message = responseJson["message"].Value<string>();
                } catch { }
            }

            return message;
        }

        private HttpWebRequest CreateHttpWebRequest(ExceptionlessConfiguration config, string endPoint) {
            var request = (HttpWebRequest)WebRequest.Create(String.Concat(config.GetServiceEndPoint(), endPoint));
            request.AddAuthorizationHeader(config);
            request.SetUserAgent(config.UserAgent);
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.None;

            try {
                request.UseDefaultCredentials = true;
                //    if (Credentials != null)
                //        request.Credentials = Credentials;
            } catch (Exception) {}

            return request;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ConfigureServicePointManagerSettings() {
            try {
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            } catch (Exception ex) {
                Trace.WriteLine(String.Format("An error occurred while configuring SSL certificate validation. Exception: {0}", ex));
            }
        }
    }
}