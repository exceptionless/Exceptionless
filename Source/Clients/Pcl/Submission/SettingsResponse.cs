#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Submission {
    public class SettingsResponse {
        public SettingsResponse(bool success, SettingsDictionary settings = null, int settingsVersion = -1, string errorMessage = null) {
            Success = success;
            Settings = settings;
            SettingsVersion = settingsVersion;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; private set; }
        public SettingsDictionary Settings { get; private set; }
        public int SettingsVersion { get; private set; }
        public string ErrorMessage { get; private set; }
    }
}