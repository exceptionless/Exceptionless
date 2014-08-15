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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Exceptionless.Models.Collections;

namespace Exceptionless.SampleWpf {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            ExceptionlessClient.Default.SubmittingEvent += OnSubmittingEvent;
            ExceptionlessClient.Default.Configuration.Settings.Changed += SettingsOnChanged;
        }

        private void SettingsOnChanged(object sender, ChangedEventArgs<KeyValuePair<string, string>> args) {
            WriteLog("Configuration updated.");
        }

        private void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            e.Event.Data["BaseDirectory"] = AppDomain.CurrentDomain.BaseDirectory;
            if (e.Event.Message == "Important Exception")
                e.Event.Tags.Add("Important");

            if (!String.IsNullOrEmpty(e.Event.ReferenceId))
                WriteLog(String.Format("Submitting Event: {0}{1}", e.Event.ReferenceId, Environment.NewLine));
            else
                WriteLog("Submitting Event");
        }

        private void WriteLog(string message) {
            if (logTextBox.Dispatcher.CheckAccess())
                logTextBox.AppendText(message);
            else
                logTextBox.Dispatcher.Invoke(new Action<string>(logTextBox.AppendText), message);
        }

        private void OnGenerateException(object sender, RoutedEventArgs e) {
            //try to open a file
            File.ReadAllText("somefile2.txt");
        }

        private void OnGenerateNestedException(object sender, RoutedEventArgs e) {
            try {
                //try to open a file
                File.ReadAllText("somefile.txt");
                //simulate filenotfound exception
                //throw new System.IO.FileNotFoundException("The file could not be found.", "SomeFile.txt");
            } catch (Exception ex) {
                throw new ApplicationException(
                    "An error has occurred and I have wrapped it inside of this ApplicationException.", ex);
            }
        }

        private void OnIgnoredException(object sender, RoutedEventArgs e) {
            throw new InvalidOperationException("Some fake exception we will check for and ignore.");
        }

        private void OnRandomException(object sender, RoutedEventArgs e) {
            string path = Path.GetRandomFileName();

            //try to open a file
            //simulate filenotfound exception
            File.ReadAllText(path);
        }

        private void OnImportDemoReports(object sender, RoutedEventArgs e) {}

        private void OnProcessQueue(object sender, RoutedEventArgs e) {
            ExceptionlessClient.Default.ProcessQueueAsync();
        }

        private void OnGenerateThreadException(object sender, RoutedEventArgs e) {
            var t = new Thread(() => {
                // will cause application to close
                throw new Exception("TEST!");
            });

            t.Start();
        }

        private void OnHandledThreadException(object sender, RoutedEventArgs e) {
            var t = new Thread(() => {
                try {
                    throw new Exception("TEST!");
                } catch (Exception ex) {
                    ex.ToExceptionless().Submit();
                }
            });

            t.Start();
        }

        private bool _isFloodingQueue = false;
        private CancellationTokenSource _floodQueueTokenSource;

        private void OnFloodQueue(object sender, RoutedEventArgs e) {
            _isFloodingQueue = !_isFloodingQueue;
            FloodQueueMenuItem.Header = _isFloodingQueue ? "Stop Flooding Queue" : "Start Flooding Queue";

            if (_isFloodingQueue) {
                _floodQueueTokenSource = new CancellationTokenSource();
                CancellationToken token = _floodQueueTokenSource.Token;
                SendContinuous(10, token);
            } else if (_floodQueueTokenSource != null)
                _floodQueueTokenSource.Cancel();
        }

        private static void SendContinuous(int delay, CancellationToken token) {
            Task.Factory.StartNew(delegate {
                while (true) {
                    if (token.IsCancellationRequested)
                        break;

                    SendOne();
                    Thread.Sleep(delay);
                }
            }, token);
        }

        private static void SendOne() {
            try {
                throw new ApplicationException("Blah2");
            } catch (Exception ex) {
                ex.ToExceptionless().Submit();
            }
        }
    }
}