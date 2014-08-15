#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Exceptionless.Models.Collections;
using Tester;

namespace Exceptionless.SampleWindows {
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
            ExceptionlessClient.Default.SubmittingEvent += OnSubmittingEvent;
            ExceptionlessClient.Default.Configuration.Settings.Changed += SettingsOnChanged;
        }

        private void SettingsOnChanged(object sender, ChangedEventArgs<KeyValuePair<string, string>> args) {
            if (logTextBox.InvokeRequired) {
                logTextBox.Invoke(new EventHandler<ChangedEventArgs<KeyValuePair<string, string>>>(SettingsOnChanged), sender, args);
                return;
            }

            logTextBox.AppendText("Configuration Updated.");
        }

        private void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            if (logTextBox.InvokeRequired) {
                logTextBox.Invoke(new EventHandler<EventSubmittingEventArgs>(OnSubmittingEvent), sender, e);
                return;
            }

            e.Event.Data["BaseDirectory"] = AppDomain.CurrentDomain.BaseDirectory;
            if (e.Event.Message == "Important Exception")
                e.Event.Tags.Add("Important");

            if (!String.IsNullOrEmpty(e.Event.ReferenceId))
                logTextBox.AppendText(String.Format("Submitting Event: {0}{1}", e.Event.ReferenceId, Environment.NewLine));
            else
                logTextBox.AppendText("Submitting Event");

            statusLabel.Text = "Submitting Message";
        }

        private void generateExceptionToolStripMenuItem_Click(object sender, EventArgs e) {
            //try to open a file
            string buffer = File.ReadAllText("somefile2.txt");
        }

        private void processQueueToolStripMenuItem_Click(object sender, EventArgs e) {
            ExceptionlessClient.Default.ProcessQueueAsync();
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
            using (ExceptionlessClient.Default.Configuration.DefaultTags.Add("Important")) {
                // Doing really important work here like maybe processing an order.
                throw new OverflowException("Bad things man.");
            }
        }

        private void multipleExceptionsToolStripMenuItem_Click(object sender, EventArgs e) {
            var multiple = new MultipleForm();
            multiple.ShowDialog(this);

            decimal count = multiple.NumericUpDown.Value;

            for (int i = 0; i < count; i++) {
                new ApplicationException("Multiple Crash Test.").ToExceptionless().SetUserDescription("some@email.com", "Testing multiple crash reports. " + i).Submit();
                //r.Description = "Testing multiple crash reports.";
                //r.EmailAddress = "my@email.com";
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