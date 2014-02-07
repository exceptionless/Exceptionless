#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.ComponentModel;

namespace Exceptionless.Net {
    internal class RestRequestCompletedEventArgs : AsyncCompletedEventArgs {
        public RestRequestCompletedEventArgs(Exception error, bool cancelled, object userState, RestState restState)
            : base(error, cancelled, userState) {
            RestState = restState;
        }

        public RestState RestState { get; private set; }

        public TResponse GetResponse<TResponse>() {
            if (RestState == null || RestState.ResponseData == null)
                return default(TResponse);

            return (TResponse)RestState.ResponseData;
        }

        public bool HasError() {
            return Error != null || RestState.Error != null;
        }
    }
}