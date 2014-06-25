#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows.Forms;
using Exceptionless.Dialogs;
using Exceptionless.Windows.Extensions;

namespace Exceptionless {
    public static class ExceptionlessWindowsExtensions {
        public static void Register(this ExceptionlessClient client, bool showDialog = true) {
            client.Startup();
            client.RegisterApplicationThreadExceptionHandler();

            if (!showDialog)
                return;

            client.SubmittingEvent -= OnSubmittingEvent;
            client.SubmittingEvent += OnSubmittingEvent;
        }

        public static void Unregister(this ExceptionlessClient client) {
            client.Shutdown();
            client.UnregisterApplicationThreadExceptionHandler();
            
            client.SubmittingEvent -= OnSubmittingEvent;
        }

        private static void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            // ev.ExceptionlessClientInfo.Platform = ".NET Windows";

            if (!e.IsUnhandledError)
                return;

            var dialog = new CrashReportForm(e.Client, e.Event);
            e.Cancel = dialog.ShowDialog() != DialogResult.OK;
        }
    }
}