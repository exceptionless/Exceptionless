#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Net;
using Exceptionless.Configuration;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Submission.Net;

namespace Exceptionless.Submission {
    public class DefaultSubmissionClient : ISubmissionClient {
        public SubmissionResponse Submit(IEnumerable<Event> events, ExceptionlessConfiguration config, IJsonSerializer serializer) {
            HttpWebRequest client = WebRequest.CreateHttp(String.Concat(config.GetServiceEndPoint(), "events"));
            client.SetUserAgent(config.UserAgent);
            client.AddAuthorizationHeader(config);

            var data = serializer.Serialize(events);

            HttpWebResponse response;
            try {
                response = client.PostJsonAsync(data).Result;
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

            return new SubmissionResponse((int)response.StatusCode, response.StatusCode == HttpStatusCode.Accepted ? null : response.GetResponseText());
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration configuration, IJsonSerializer serializer) {
            HttpWebRequest client = WebRequest.CreateHttp(String.Concat(configuration.GetServiceEndPoint(), "projects/config"));
            client.AddAuthorizationHeader(configuration);

            HttpWebResponse response;
            try {
                response = client.GetJsonAsync().Result;
            } catch (Exception ex) {
                var message = String.Concat("Unable to retrieve configuration settings. Exception: ", ex.GetMessage());
                return new SettingsResponse(false, message: message);
            }

            if (response == null || response.StatusCode != HttpStatusCode.OK)
                return new SettingsResponse(false, message: "Unable to retrieve configuration settings.");

            var json = response.GetResponseText();
            if (String.IsNullOrWhiteSpace(json))
                return new SettingsResponse(false, message: "Invalid configuration settings.");

            var settings = serializer.Deserialize<ClientConfiguration>(json);
            return new SettingsResponse(true, settings.Settings, settings.Version);
        }
    }
}
