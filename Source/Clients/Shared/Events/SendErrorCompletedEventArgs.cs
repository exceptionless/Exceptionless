#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.ComponentModel;

namespace Exceptionless {
#if !SILVERLIGHT && !PORTABLE40
    [Serializable]
#endif
    public class SendErrorCompletedEventArgs : AsyncCompletedEventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="SendErrorCompletedEventArgs" /> class.
        /// </summary>
        /// <param name="errorId">The error response.</param>
        /// <param name="e">The exception.</param>
        /// <param name="canceled">if set to <c>true</c> then canceled.</param>
        /// <param name="state">The state object.</param>
        public SendErrorCompletedEventArgs(string errorId, Exception e, bool canceled, object state) : base(e, canceled, state) {
            ErrorId = errorId;
        }

        /// <summary>
        /// Gets the error response.
        /// </summary>
        /// <value>The error response.</value>
        public string ErrorId { get; private set; }
    }
}