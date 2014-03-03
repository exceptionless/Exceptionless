using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Models;

#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

namespace Exceptionless.Submission {
    public interface ISubmissionClient {
        Task<SubmissionResponse> SubmitAsync(IEnumerable<Error> errors, Configuration configuration);

        Task<SettingsResponse> GetSettingsAsync(Configuration configuration);
    }

    public class SettingsResponse {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public SettingsDictionary Settings { get; private set; }
        public int SettingsVersion { get; private set; }
    }

    public class SubmissionResponse {
        public SubmissionResponse(bool success, int settingsVersion = -1, string errorMessage = null) {
            Success = success;
            SettingsVersion = settingsVersion;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; private set; }
        public int SettingsVersion { get; private set; }
        public string ErrorMessage { get; private set; }
    }
}