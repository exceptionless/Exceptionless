#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Plugins {
    public interface IExceptionlessPlugin {
        /// <summary>
        /// Is called after the error object is created and can be used to add any essential information to the error report
        /// and set the client info.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="error">Error report that was created.</param>
        /// <param name="exception">The exception that the error report was created from.</param>
        void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception);

        /// <summary>
        /// Add any additional non-essential information to the error report.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="error">Error report that was created.</param>
        void AddDefaultInformation(ExceptionlessPluginContext context, Error error);

        /// <summary>
        /// Determines if this plugin supports showing a submission UI.
        /// </summary>
        bool SupportsShowingUnhandledErrorSubmissionUI { get; }

        /// <summary>
        /// Shows the submission UI for GUI clients and returns true if the error should be sent to the server.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="error">Error report that was created.</param>
        /// <returns>True if the error should be sent to the server.</returns>
        bool ShowUnhandledErrorSubmissionUI(ExceptionlessPluginContext context, Error error);
    }
}