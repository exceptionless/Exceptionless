#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows.Forms;
using Exceptionless.Extras.Utility;
using Exceptionless.Models;

namespace Exceptionless.Dialogs {
    public sealed partial class CrashReportForm : Form {
        public Event Event { get; private set; }

        public CrashReportForm(Event ev) {
            InitializeComponent();

            Event = ev;
            Text = String.Format("{0} Error", AssemblyHelper.GetAssemblyTitle());
            InformationHeaderLabel.Text = String.Format("{0} has encountered a problem and needs to close.  We are sorry for the inconvenience.", AssemblyHelper.GetAssemblyTitle());

            // TODO: Implement this once the client has persisted storage.
            var userInfo = ev.GetUserIdentity();
            if (userInfo != null && !String.IsNullOrEmpty(userInfo.Identity))
                EmailAddressTextBox.Text = userInfo.Identity;
            //else
            //    EmailAddressTextBox.Text = ExceptionlessClient.Default.LocalConfiguration.EmailAddress;

            DescriptionTextBox.Text = Event.GetUserDescription();
        }

        private void ExitButton_Click(object sender, EventArgs e) {
            Close();
        }

        private void OnSubmitReportButtonClick(object sender, EventArgs e) {
            Cursor = Cursors.WaitCursor;
            SendReportButton.Enabled = false;
            ExitButton.Enabled = false;

            Event.SetUserIdentity(EmailAddressTextBox.Text);
            Event.AddUserDescription(DescriptionTextBox.Text);

            Cursor = Cursors.Default;
            SendReportButton.Enabled = true;
            ExitButton.Enabled = true;
            Close();
        }
    }
}