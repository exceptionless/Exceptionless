#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Exceptionless.SampleWindows {
    internal static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main() {
            ExceptionlessClient.Default.Configuration.UseTraceLogger();
            ExceptionlessClient.Default.Register();
            ExceptionlessClient.Default.SubmittingEvent += OnSubmittingEvent;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try {
                Application.Run(new MainForm());
            } catch (InvalidOperationException) {
                Debug.WriteLine("Got an InvalidOperationException.");
            }
        }

        private static void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            e.Event.Tags.Add("ExtraTag");

            var exception = e.EnrichmentContextData.GetException();
            if (exception != null && exception.GetType() == typeof(InvalidOperationException))
                e.Cancel = true;
        }
    }
}