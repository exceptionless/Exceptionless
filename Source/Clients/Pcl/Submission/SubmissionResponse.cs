#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Submission {
    public class SubmissionResponse {
        public SubmissionResponse(bool success, int settingsVersion = -1, Exception exception = null, string message = null) {
            Success = success;
            SettingsVersion = settingsVersion;
            Exception = exception;
            Message = message;
        }

        public bool Success { get; private set; }
        public int SettingsVersion { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }
    }
}