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
using Exceptionless.Windows;

namespace Exceptionless {
    public static class ExceptionlessManagerWindowsExtensions {
        public static void Register(this ExceptionlessClient client, bool showDialog = true) {
            client..RegisterPlugin(new ExceptionlessWindowsPlugin(showDialog));
            client.Startup();

            if (showDialog) {
                client.UnhandledExceptionReporting -= UnhandledExceptionReporting;
                client.UnhandledExceptionReporting += UnhandledExceptionReporting;
            }

            Application.ThreadException -= OnApplicationThreadException;
            Application.ThreadException += OnApplicationThreadException;
        }

        public static void Unregister(this ExceptionlessClient client) {
            client.Shutdown();

            client.UnhandledExceptionReporting -= UnhandledExceptionReporting;

            Application.ThreadException -= OnApplicationThreadException;
        }

        private static void OnApplicationThreadException(object sender, ThreadExceptionEventArgs e) {
            ExceptionlessClient.Default.ProcessUnhandledException(e.Exception, "ApplicationThreadException");
        }

        private static void UnhandledExceptionReporting(object sender, UnhandledExceptionReportingEventArgs e) {
            var dialog = new CrashReportForm(e.Event);
            DialogResult result = dialog.ShowDialog();
            e.Cancel = result == DialogResult.Cancel;
        }
    }
}