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

namespace Exceptionless.Windows {
    internal class ExceptionlessWindowsPlugin : ExceptionlessPlugin {
        private readonly bool _showDialog = true;

        public ExceptionlessWindowsPlugin(bool showDialog = true) {
            _showDialog = showDialog;
        }

        public override void AfterCreated(ExceptionlessPluginContext context, Event ev, Exception exception) {
            base.AfterCreated(context, ev, exception);

            ev.ExceptionlessClientInfo.Platform = ".NET Windows";
        }

        public override bool SupportsShowingUnhandledErrorSubmissionUI { get { return true; } }

        public override bool ShowUnhandledErrorSubmissionUI(ExceptionlessPluginContext context, Event ev) {
            if (!_showDialog)
                return true;

            return ShowDialog(ev);
        }

        private bool ShowDialog(Event ev) {
            var dialog = new CrashReportForm(ev);
            DialogResult result = dialog.ShowDialog();
            return result == DialogResult.OK;
        }
    }
}