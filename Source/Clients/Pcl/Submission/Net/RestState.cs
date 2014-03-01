#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Net;

namespace Exceptionless.Net {
    internal class RestState {
        public RestState() {
            Request = null;
            Response = null;
            Method = "GET";
        }

        public Uri EndPoint { get; set; }
        public HttpWebRequest Request { get; set; }
        public HttpWebResponse Response { get; set; }

        public string Method { get; set; }

        public object RequestData { get; set; }
        public Type RequestType { get; set; }

        public object ResponseData { get; set; }
        public Type ResponseType { get; set; }

        public Exception Error { get; set; }

        public object UserToken { get; set; }
        public bool IsCancelled { get; set; }

        public bool IsPost() {
            return String.Equals("POST", Method, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsGet() {
            return String.Equals("GET", Method, StringComparison.OrdinalIgnoreCase);
        }
    }
}