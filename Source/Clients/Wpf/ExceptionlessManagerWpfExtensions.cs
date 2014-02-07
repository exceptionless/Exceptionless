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
using Exceptionless.Wpf;

namespace Exceptionless {
    public static class ExceptionlessManagerWpfExtensions {
        public static void Register(this ExceptionlessClient client, bool showDialog = true) {
            if (client == null)
                throw new ArgumentException("Exceptionless Manager cannot be null.", "client");

            client.RegisterPlugin(new ExceptionlessWpfPlugin(showDialog));
            client.Startup();

            if (Application.Current == null)
                return;

            Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            System.Windows.Forms.Application.ThreadException -= OnApplicationThreadException;
            System.Windows.Forms.Application.ThreadException += OnApplicationThreadException;
        }

        public static void Unregister(this ExceptionlessClient client) {
            client.Shutdown();

            if (Application.Current == null)
                return;

            Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            ExceptionlessClient.Current.ProcessUnhandledException(e.Exception, "DispatcherUnhandledException");
            e.Handled = true;
        }

        private static void OnApplicationThreadException(object sender, ThreadExceptionEventArgs e) {
            ExceptionlessClient.Current.ProcessUnhandledException(e.Exception, "ApplicationThreadException");
        }
    }
}