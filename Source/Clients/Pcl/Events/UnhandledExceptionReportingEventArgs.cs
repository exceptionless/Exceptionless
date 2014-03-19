#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless {
    public class UnhandledExceptionReportingEventArgs : EventArgs {
        public UnhandledExceptionReportingEventArgs(Exception exception, Event data) {
            Exception = exception;
            Event = data;
            ShouldShowUI = true;
        }

        /// <summary>
        /// The error that is about to be submitted.  Changes can be made to the error before it is submitted or it can be
        /// cancelled.
        /// </summary>
        /// <value>The case error.</value>
        public Event Event { get; private set; }

        /// <summary>
        /// The unhandled exception that triggered the event.
        /// </summary>
        /// <value>The exception.</value>
        public Exception Exception { get; private set; }

        /// <summary>
        /// If set to true, the error will not be reported.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Controls whether a UI should be shown or not in clients that support showing a UI (ie. WPF and WinForms).
        /// </summary>
        public bool ShouldShowUI { get; set; }
    }
}