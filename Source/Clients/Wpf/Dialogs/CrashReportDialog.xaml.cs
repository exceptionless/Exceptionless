#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows;
using System.Windows.Input;
using Exceptionless.Extras.Utility;
using Exceptionless.Models;

namespace Exceptionless.Dialogs {
    /// <summary>
    /// Interaction logic for CrashReportDialog.xaml
    /// </summary>
    public partial class CrashReportDialog : Window {
        public Event Event { get; internal set; }

        public CrashReportDialog(Event ev) {
            InitializeComponent();

            Event = ev;
            Title = String.Format("{0} Error", AssemblyHelper.GetAssemblyTitle());
            InformationHeaderLabel.Text = String.Format("{0} has encountered a problem and needs to close.  We are sorry for the inconvenience.", AssemblyHelper.GetAssemblyTitle());

            // TODO: Implement this once the client has persisted storage.
            var userInfo = ev.GetUserInfo();
            if (userInfo != null && !String.IsNullOrEmpty(userInfo.Identity))
                EmailAddressTextBox.Text = userInfo.Identity;
            //else
            //    EmailAddressTextBox.Text = ExceptionlessClient.Default.LocalConfiguration.EmailAddress;

            DescriptionTextBox.Text = ev.GetUserDescription();
        }

        private void OnSubmitReportButtonClick(object sender, RoutedEventArgs e) {
            Cursor = Cursors.Wait;

            SubmitReportButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            Event.AddUserInfo(EmailAddressTextBox.Text);
            Event.AddUserDescription(DescriptionTextBox.Text);

            Cursor = Cursors.Arrow;
            SubmitReportButton.IsEnabled = true;
            CancelButton.IsEnabled = true;

            DialogResult = true;
            Close();
        }
    }
}