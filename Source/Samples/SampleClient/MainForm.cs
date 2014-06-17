#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Exceptionless.Models;
using Tester;

namespace Exceptionless.SampleWinForms {
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
            ExceptionlessClient.Default.SendingError += OnFeedbackSubmitting;
            ExceptionlessClient.Default.SendErrorCompleted += OnFeedbackCompleted;
            ExceptionlessClient.Default.ConfigurationUpdated += OnConfigurationUpdated;
        }

        private void OnConfigurationUpdated(object sender, ConfigurationUpdatedEventArgs e) {
            if (logTextBox.InvokeRequired) {
                logTextBox.Invoke(new EventHandler<ConfigurationUpdatedEventArgs>(OnConfigurationUpdated), sender, e);
                return;
            }

            var sb = new StringBuilder();

            if (e.Configuration != null) {
                sb.AppendLine("Configuration Updated");
                sb.AppendLine(String.Format("    Version: {0} ", e.Configuration.Version));
                //if (!String.IsNullOrEmpty(e.Configuration.ExceptionMessage))
                //    sb.AppendLine("    Error: " + e.Configuration.ExceptionMessage);
            } else
                sb.AppendLine("Configuration Updated: Response is {null}");

            logTextBox.AppendText(sb.ToString());
        }

        private void OnFeedbackCompleted(object sender, SendErrorCompletedEventArgs e) {
            if (logTextBox.InvokeRequired) {
                logTextBox.Invoke(new EventHandler<SendErrorCompletedEventArgs>(OnFeedbackCompleted), sender, e);
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine("Submit Completed: " + e.ErrorId);

            if (e.Event != null)
                sb.AppendLine("    Event: " + e.Event.Message);
            //else if (e.ReportResponse.Status == ResponseStatusType.Event)
            //    sb.AppendLine("    Event: " + e.ReportResponse.ExceptionMessage);

            logTextBox.AppendText(sb.ToString());

            statusLabel.Text = e.Event != null ? "Submit Event" : "Submit Completed";
        }

        private void OnFeedbackSubmitting(object sender, ErrorModelEventArgs e) {
            if (logTextBox.InvokeRequired) {
                logTextBox.Invoke(new EventHandler<ErrorModelEventArgs>(OnFeedbackSubmitting), sender, e);
                return;
            }

            string message = "Submitting Message: " + e.Event.Id + Environment.NewLine;
            logTextBox.AppendText(message);

            e.Event.ExtendedData.Add("BaseDirectory", AppDomain.CurrentDomain.BaseDirectory);
            statusLabel.Text = "Submitting Message";

            if (e.Event.Message == "Important Exception")
                e.Event.Tags.Add("Important");
        }

        private void generateExceptionToolStripMenuItem_Click(object sender, EventArgs e) {
            //try to open a file
            string buffer = File.ReadAllText("somefile2.txt");
            //simulate filenotfound exception
            //throw new System.IO.FileNotFoundException("The file could not be found.", "SomeFile.txt");
        }

        private void processQueueToolStripMenuItem_Click(object sender, EventArgs e) {
            ExceptionlessClient.Default.ProcessQueueAsync();
        }

        private void updateConfigurationToolStripMenuItem_Click(object sender, EventArgs e) {
            ExceptionlessClient.Default.UpdateConfigurationAsync(true);
        }

        private void randomExceptionToolStripMenuItem_Click(object sender, EventArgs e) {
            string path = Path.GetRandomFileName();

            //try to open a file
            //simulate filenotfound exception
            string buffer = File.ReadAllText(path);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e) {
            try {
                //try to open a file
                string buffer = File.ReadAllText("somefile.txt");
                //simulate filenotfound exception
                //throw new System.IO.FileNotFoundException("The file could not be found.", "SomeFile.txt");
            } catch (Exception ex) {
                throw new ApplicationException(
                    "An ev has occurred and I have wrapped it inside of this ApplicationException.", ex);
            }
        }

        private void ignoredExceptionToolStripMenuItem_Click(object sender, EventArgs e) {
            throw new InvalidOperationException("Some fake exception we will check for and ignore.");
        }

        private void importantExceptionToolStripMenuItem_Click(object sender, EventArgs e) {
            using (ExceptionlessClient.Default.Tags.Add("Important")) {
                // Doing really important work here like maybe processing an order.
                throw new OverflowException("Bad things man.");
            }
        }

        private void multipleExceptionsToolStripMenuItem_Click(object sender, EventArgs e) {
            var multiple = new MultipleForm();
            multiple.ShowDialog(this);

            decimal count = multiple.NumericUpDown.Value;

            for (int i = 0; i < count; i++) {
                Event r = ExceptionlessClient.Default.CreateError(new ApplicationException("Multiple Crash Test."));
                r.Message = "Testing multiple crash reports. " + i;
                //r.Description = "Testing multiple crash reports.";
                //r.EmailAddress = "my@email.com";

                ExceptionlessClient.Default.SubmitError(r);
            }
        }

        private void showFilterFormToolStripMenuItem_Click(object sender, EventArgs e) {
            var filterForm = new FilterForm();
            filterForm.Show();
        }

        private void MainForm_Load(object sender, EventArgs e) {}

        private void importDemoReportsToolStripMenuItem_Click(object sender, EventArgs e) {
            // find sample folder 
            string folder = SampleLoader.FindSamples();
            if (String.IsNullOrEmpty(folder)) {
                logTextBox.AppendText("Event: Samples directory not found.");
                return;
            }

            var loader = new SampleLoader(folder);
            loader.Load();
        }
    }
}