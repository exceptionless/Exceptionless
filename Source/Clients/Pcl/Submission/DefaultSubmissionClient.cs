#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Submission.Net;

namespace Exceptionless.Submission {
    public class DefaultSubmissionClient : ISubmissionClient {
        private const string DEFAULT_CONTENT_TYPE = "application/json";

        public Task<SubmissionResponse> SubmitAsync(IEnumerable<Error> errors, Configuration configuration) {
            var submitUrl = String.Concat(configuration.ServerUrl, "error");

            HttpWebRequest client = WebRequest.CreateHttp(submitUrl);
            client.Accept = client.ContentType = DEFAULT_CONTENT_TYPE;
            client.Method = "POST";

            client.Headers[HttpRequestHeader.Authorization] = CreateAuthorizationHeader(configuration).ToString();

            var serializer = DependencyResolver.Current.GetJsonSerializer();

            // TODO: We only support one error right now..
            byte[] data = Encoding.UTF8.GetBytes(serializer.Serialize(errors.FirstOrDefault()));
            using (var stream = client.GetRequestStreamAsync().Result) {
                stream.Write(data, 0, data.Length);
            }

            return client.GetResponseAsync().ContinueWith(r => {
                // TODO: We need to break down the aggregate exceptions error message into something usable.
                if (r.IsFaulted || r.Exception != null)
                    return new SubmissionResponse(false, errorMessage: r.Exception != null ? r.Exception.Message : null);

                var response = (HttpWebResponse)r.Result;

                int settingsVersion;
                if (!Int32.TryParse(response.Headers[ExceptionlessHeaders.ConfigurationVersion], out settingsVersion))
                    settingsVersion = -1;

                // TODO: Add support for sending errors later (e.g., Suspended Account. Invalid API Key).

                if (!response.IsSuccessStatusCode() || response.StatusCode != HttpStatusCode.Created)
                    return new SubmissionResponse(false, settingsVersion, response.GetResponseText());

                return new SubmissionResponse(true, settingsVersion);
            });
        }

        public Task<SettingsResponse> GetSettingsAsync(Configuration configuration) {
            throw new NotImplementedException();
        }

        private AuthorizationHeader CreateAuthorizationHeader(Configuration configuration) {
            return new AuthorizationHeader {
                Scheme = ExceptionlessHeaders.Basic,
                ParameterText = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", "client", configuration.ApiKey)))
            };
        }

        internal static class ExceptionlessHeaders {
            public const string Basic = "Basic";
            public const string ConfigurationVersion = "v";
        }
    }
}
