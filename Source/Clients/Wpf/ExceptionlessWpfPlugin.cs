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
using Exceptionless.Plugins;

namespace Exceptionless.Wpf {
    internal class ExceptionlessWpfPlugin : ExceptionlessPlugin {
        private readonly bool _showDialog = true;

        public ExceptionlessWpfPlugin(bool showDialog = true) {
            _showDialog = showDialog;
        }

        public override void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception) {
            base.AfterCreated(context, error, exception);

            error.ExceptionlessClientInfo.Platform = ".NET WPF";
        }

        public override bool SupportsShowingUnhandledErrorSubmissionUI { get { return true; } }

        public override bool ShowUnhandledErrorSubmissionUI(ExceptionlessPluginContext context, Error error) {
            if (!_showDialog)
                return true;

            bool shouldSend;

            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                shouldSend = (bool)Application.Current.Dispatcher.Invoke(new Func<Error, bool>(ShowDialog), DispatcherPriority.Send, error);
            else
                shouldSend = ShowDialog(error);

            return shouldSend;
        }

        private static bool ShowDialog(Error e) {
            var dialog = new CrashReportDialog(e);
            bool? result = dialog.ShowDialog();
            return result.HasValue && result.Value;
        }
    }
}