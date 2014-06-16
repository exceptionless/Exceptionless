#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Windows;
using System.Windows.Input;
using Exceptionless.Models;
using Exceptionless.Utility;

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
            headerText.Text = String.Format("{0} has encountered a problem and needs to close.  We are sorry for the inconvenience.", AssemblyHelper.GetAssemblyTitle());

            emailAddressTextBox.Text = String.IsNullOrEmpty(ev.UserEmail)
                ? ExceptionlessClient.Default.LocalConfiguration.EmailAddress
                : ev.UserEmail;

            descriptionTextBox.Text = ev.UserDescription;
        }

        private void sendReportButton_Click(object sender, RoutedEventArgs e) {
            Cursor = Cursors.Wait;

            sendReportButton.IsEnabled = false;
            cancelButton.IsEnabled = false;

            Event.UserEmail = emailAddressTextBox.Text;
            Event.UserDescription = descriptionTextBox.Text;

            ExceptionlessClient.Default.SubmitError(Event);

            Cursor = Cursors.Arrow;
            sendReportButton.IsEnabled = true;
            cancelButton.IsEnabled = true;

            DialogResult = true;
            Close();
        }
    }
}