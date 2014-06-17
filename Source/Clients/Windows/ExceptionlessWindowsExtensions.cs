#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Threading;
using System.Windows.Forms;
using Exceptionless.Dialogs;
using Exceptionless.Enrichments;

namespace Exceptionless {
    public static class ExceptionlessWindowsExtensions {
        public static void Register(this ExceptionlessClient client, bool showDialog = true) {
            //client.Startup();

            if (showDialog) {
                client.SubmittingEvent -= OnSubmittingEvent;
                client.SubmittingEvent += OnSubmittingEvent;
            }

            Application.ThreadException -= OnApplicationThreadException;
            Application.ThreadException += OnApplicationThreadException;
        }

        public static void Unregister(this ExceptionlessClient client) {
            //client.Shutdown();
            
            client.SubmittingEvent -= OnSubmittingEvent;
            Application.ThreadException -= OnApplicationThreadException;
        }

        private static void OnApplicationThreadException(object sender, ThreadExceptionEventArgs e) {
            var contextData = new ContextData();
            contextData.SetUnhandled();
            contextData.SetSubmissionMethod("ApplicationThreadException");

            e.Exception.ToExceptionless(contextData).Submit();
        }
        
        private static void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            // ev.ExceptionlessClientInfo.Platform = ".NET Windows";

            if (!e.EnrichmentContextData.IsUnhandled)
                return;

            var dialog = new CrashReportForm(e.Event);
            DialogResult result = dialog.ShowDialog();
            e.Cancel = result == DialogResult.OK;
        }
    }
}