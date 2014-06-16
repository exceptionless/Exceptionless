#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Net;

namespace Exceptionless.Submission {
    public class SubmissionResponse {
        public SubmissionResponse(int statusCode, int settingsVersion = -1, string message = null) {
            StatusCode = statusCode;
            SettingsVersion = settingsVersion;
            Message = message;

            Success = (HttpStatusCode)statusCode == HttpStatusCode.Accepted;
            BadRequest = (HttpStatusCode)statusCode == HttpStatusCode.BadRequest;
            ServiceUnavailable = (HttpStatusCode)statusCode == HttpStatusCode.ServiceUnavailable;
            PaymentRequired = (HttpStatusCode)statusCode == HttpStatusCode.PaymentRequired;
            UnableToAuthenticate = (HttpStatusCode)statusCode == HttpStatusCode.Unauthorized || (HttpStatusCode)statusCode == HttpStatusCode.Forbidden;
            NotFound = (HttpStatusCode)statusCode == HttpStatusCode.NotFound;
        }

        public bool Success { get; private set; }
        public bool BadRequest { get; private set; }
        public bool ServiceUnavailable { get; private set; }
        public bool PaymentRequired { get; private set; }
        public bool UnableToAuthenticate { get; private set; }
        public bool NotFound { get; private set; }

        public int StatusCode { get; private set; }
        public int SettingsVersion { get; private set; }
        public string Message { get; private set; }
    }
}