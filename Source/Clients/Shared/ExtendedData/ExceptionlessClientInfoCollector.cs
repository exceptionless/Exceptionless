#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;
using Exceptionless.Models.Legacy;

namespace Exceptionless.ExtendedData {
    public static class ExceptionlessClientInfoCollector {
        public static ExceptionlessClientInfo Collect(ExceptionlessClient client, bool includePrivateInfo = true) {
            if (includePrivateInfo) {
                return new ExceptionlessClientInfo {
                    Version = ThisAssembly.AssemblyInformationalVersion,
                    InstallIdentifier = client.LocalConfiguration.InstallIdentifier.ToString(),
                    InstallDate = client.LocalConfiguration.InstallDate,
                    StartCount = client.LocalConfiguration.StartCount,
                    SubmitCount = client.LocalConfiguration.SubmitCount,
                    Platform = ".NET"
                };
            }

            return new ExceptionlessClientInfo {
                Version = ThisAssembly.AssemblyInformationalVersion,
                InstallDate = client.LocalConfiguration.InstallDate,
                StartCount = client.LocalConfiguration.StartCount,
                SubmitCount = client.LocalConfiguration.SubmitCount,
                Platform = ".NET"
            };
        }
    }
}