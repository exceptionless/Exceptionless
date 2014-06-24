#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows;
using System.Windows.Threading;
using Exceptionless.Dialogs;
using Exceptionless.Models;
using Exceptionless.Wpf.Extensions;

namespace Exceptionless {
    public static class ExceptionlessWpfExtensions {
        public static void Register(this ExceptionlessClient client, bool showDialog = true) {
            client.Startup();
            client.RegisterApplicationThreadExceptionHandler();
            client.RegisterApplicationDispatcherUnhandledExceptionHandler();

            if (!showDialog)
                return;

            client.SubmittingEvent -= OnSubmittingEvent;
            client.SubmittingEvent += OnSubmittingEvent;
        }

        public static void Unregister(this ExceptionlessClient client) {
            client.Shutdown();
            client.UnregisterApplicationThreadExceptionHandler();
            client.UnregisterApplicationDispatcherUnhandledExceptionHandler();

            client.SubmittingEvent -= OnSubmittingEvent;
        }

        private static void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            //error.ExceptionlessClientInfo.Platform = ".NET WPF";

            if (!e.IsUnhandledError)
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