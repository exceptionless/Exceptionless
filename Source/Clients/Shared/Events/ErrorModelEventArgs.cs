#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless {
    /// <summary>
    /// EventArgs derived type which holds the custom event fields
    /// </summary>
#if !SILVERLIGHT && !PORTABLE40
    [Serializable]
#endif
    public class ErrorModelEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorModelEventArgs" /> class.
        /// </summary>
        /// <param name="error">The case error.</param>
        public ErrorModelEventArgs(Error error) {
            Error = error;
        }

        /// <summary>
        /// Gets the error.
        /// </summary>
        /// <value>The error.</value>
        public Error Error { get; private set; }
    }
}