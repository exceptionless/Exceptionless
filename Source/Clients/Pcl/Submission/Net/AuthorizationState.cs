#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Net {
    internal class AuthorizationState {
        public bool IsRefresh { get; set; }
        public bool ForceUpdate { get; set; }
        public bool IsAuthenticated { get; set; }
        public Exception Error { get; set; }
        public AuthorizationHeader Header { get; set; }
    }
}