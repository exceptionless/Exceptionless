#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows.Forms;
using Exceptionless.Dialogs;
using Exceptionless.Models;
using Exceptionless.Plugins;

namespace Exceptionless.Windows {
    internal class ExceptionlessWindowsPlugin : ExceptionlessPlugin {
        private readonly bool _showDialog = true;

        public ExceptionlessWindowsPlugin(bool showDialog = true) {
            _showDialog = showDialog;
        }

        public override void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception) {
            base.AfterCreated(context, error, exception);

            error.ExceptionlessClientInfo.Platform = ".NET Windows";
        }

        public override bool SupportsShowingUnhandledErrorSubmissionUI { get { return true; } }

        public override bool ShowUnhandledErrorSubmissionUI(ExceptionlessPluginContext context, Error error) {
            if (!_showDialog)
                return true;

            return ShowDialog(error);
        }

        private bool ShowDialog(Error error) {
            var dialog = new CrashReportForm(error);
            DialogResult result = dialog.ShowDialog();
            return result == DialogResult.OK;
        }
    }
}