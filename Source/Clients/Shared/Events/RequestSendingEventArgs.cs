#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Net;

namespace Exceptionless {
#if !SILVERLIGHT && !PORTABLE40
    [Serializable]
#endif
    public class RequestSendingEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestSendingEventArgs" /> class.
        /// </summary>
        /// <param name="request">The error response.</param>
        public RequestSendingEventArgs(HttpWebRequest request) {
            Request = request;
        }

        /// <summary>
        /// Gets the request that is about to be sent.
        /// </summary>
        /// <value>The request.</value>
        public HttpWebRequest Request { get; private set; }
    }
}