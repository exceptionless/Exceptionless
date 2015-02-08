#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless {
    public static class ExceptionlessConsoleExtensions {
        public static void Register(this ExceptionlessClient client) {
            client.Startup();

            // make sure that queued events are sent when the app exits
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                client.ProcessQueue();
            };
        }

        public static void Unregister(this ExceptionlessClient client) {
            client.Shutdown();
        }
    }
}