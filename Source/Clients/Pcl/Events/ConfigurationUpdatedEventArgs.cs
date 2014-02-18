#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.ComponentModel;
using Exceptionless.Models;

namespace Exceptionless {
    public class ConfigurationUpdatedEventArgs : AsyncCompletedEventArgs {
        private readonly ClientConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.ComponentModel.AsyncCompletedEventArgs" /> class.
        /// </summary>
        /// <param name="configuration">The configuration response.</param>
        /// <param name="error">Any error that occurred during the asynchronous operation.</param>
        /// <param name="cancelled">A value indicating whether the asynchronous operation was canceled.</param>
        /// <param name="userState">
        /// The optional user-supplied state object passed to the
        /// <see cref="M:System.ComponentModel.BackgroundWorker.RunWorkerAsync(System.Object)" /> method.
        /// </param>
        public ConfigurationUpdatedEventArgs(ClientConfiguration configuration, Exception error, bool cancelled, object userState)
            : base(error, cancelled, userState) {
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the configuration response.
        /// </summary>
        /// <value>The configuration response.</value>
        public ClientConfiguration Configuration { get { return _configuration; } }
    }
}