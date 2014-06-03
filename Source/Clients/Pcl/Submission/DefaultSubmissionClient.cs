#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Net;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Submission.Net;

namespace Exceptionless.Submission {
    public class DefaultSubmissionClient : ISubmissionClient {
        public SubmissionResponse Submit(IEnumerable<Event> events, ExceptionlessConfiguration configuration, IJsonSerializer serializer) {
            HttpWebRequest client = WebRequest.CreateHttp(String.Concat(configuration.GetServiceEndPoint(), "events"));
            client.SetUserAgent(configuration.UserAgent);
            client.AddAuthorizationHeader(configuration);

            var data = serializer.Serialize(events);
            var response = client.PostJsonAsync(data).Result;
            int settingsVersion;
            if (!Int32.TryParse(response.Headers[ExceptionlessHeaders.ConfigurationVersion], out settingsVersion))
                settingsVersion = -1;

            return new SubmissionResponse((int)response.StatusCode, settingsVersion, response.StatusCode == HttpStatusCode.Accepted ? null : response.GetResponseText());
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration configuration, IJsonSerializer serializer) {
            HttpWebRequest client = WebRequest.CreateHttp(String.Concat(configuration.GetServiceEndPoint(), "projects/config"));
            client.AddAuthorizationHeader(configuration);

            var response = client.GetJsonAsync().Result;
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
