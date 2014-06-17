#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Exceptionless.Dialogs;
using Exceptionless.Enrichments;
using Exceptionless.Models;

namespace Exceptionless {
    public static class ExceptionlessWpfExtensions {
        public static void Register(this ExceptionlessClient client, bool showDialog = true) {
            //client.Startup();
            
            if (showDialog) {
                client.SubmittingEvent -= OnSubmittingEvent;
                client.SubmittingEvent += OnSubmittingEvent;
            }

            System.Windows.Forms.Application.ThreadException -= OnApplicationThreadException;
            System.Windows.Forms.Application.ThreadException += OnApplicationThreadException;

            if (Application.Current == null)
                return;

            Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

        }

        public static void Unregister(this ExceptionlessClient client) {
            //client.Shutdown();

            client.SubmittingEvent -= OnSubmittingEvent;
            System.Windows.Forms.Application.ThreadException -= OnApplicationThreadException;

            if (Application.Current != null)
                Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            var contextData = new ContextData();
            contextData.SetUnhandled();
            contextData.SetSubmissionMethod("DispatcherUnhandledException");

            e.Exception.ToExceptionless(contextData).Submit();
            e.Handled = true;
        }

        private static void OnApplicationThreadException(object sender, ThreadExceptionEventArgs e) {
            var contextData = new ContextData();
            contextData.SetUnhandled();
            contextData.SetSubmissionMethod("ApplicationThreadException");

            e.Exception.ToExceptionless(contextData).Submit();
        }

        private static void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            //error.ExceptionlessClientInfo.Platform = ".NET WPF";

            if (!e.EnrichmentContextData.IsUnhandled)
                return;

            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                e.Cancel = (bool)Application.Current.Dispatcher.Invoke(new Func<Event, bool>(ShowDialog), DispatcherPriority.Send, e.Event);
            else
                e.Cancel = ShowDialog(e.Event);
        }
         
        private static bool ShowDialog(Event e) {
            var dialog = new CrashReportDialog(e);
            bool? result = dialog.ShowDialog();
            return result.HasValue && result.Value;
        }
    }
}