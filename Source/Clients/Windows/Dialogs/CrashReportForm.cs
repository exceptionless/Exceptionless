#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows.Forms;
using Exceptionless.Models;
using Exceptionless.Utility;

namespace Exceptionless.Dialogs {
    public sealed partial class CrashReportForm : Form {
        public CrashReportForm(Error error) {
            InitializeComponent();
            Error = error;
            Text = String.Format("{0} Error", AssemblyHelper.GetAssemblyTitle());
            InformationHeaderLabel.Text = String.Format("{0} has encountered a problem and needs to close.  We are sorry for the inconvenience.", AssemblyHelper.GetAssemblyTitle());

            EmailAddressTextBox.DataBindings.Add("Text", Error, "UserEmail");
            DescriptionTextBox.DataBindings.Add("Text", Error, "UserDescription");
        }

        public Error Error { get; private set; }

        private void ExitButton_Click(object sender, EventArgs e) {
            Close();
        }

        private void SendReportButton_Click(object sender, EventArgs e) {
            Cursor = Cursors.WaitCursor;

            SendReportButton.Enabled = false;
            ExitButton.Enabled = false;

            ExceptionlessClient.Current.SubmitError(Error);

            Cursor = Cursors.Default;
            SendReportButton.Enabled = true;
            ExitButton.Enabled = true;
            Close();
        }

        private void CrashReportForm_Load(object sender, EventArgs e) {
            EmailAddressTextBox.Text = String.IsNullOrEmpty(Error.UserEmail)
                ? ExceptionlessClient.Current.LocalConfiguration.EmailAddress
                : Error.UserEmail;

            DescriptionTextBox.Text = Error.UserDescription;
        }
    }
}