#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless {
    internal class DefaultLastReferenceIdManager : ILastReferenceIdManager {
        private string _lastErrorId;

        /// <summary>
        ///     Gets the last event's client id that was submitted to the server.
        /// </summary>
        /// <returns>The client id</returns>
        public string GetLast() {
            return _lastErrorId;
        }

        /// <summary>
        ///     Clears the last error id.
        /// </summary>
        public void ClearLast() {
            _lastErrorId = String.Empty;
        }

        public void SetLast(string errorId) {
            _lastErrorId = errorId;
        }
    }
}