#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless {
    public interface ILastReferenceIdManager {
        /// <summary>
        ///     Gets the last error id that was submitted to the server.
        /// </summary>
        /// <returns>The error id</returns>
        string GetLast();

        /// <summary>
        ///     Clears the last error id.
        /// </summary>
        void ClearLast();

        /// <summary>
        ///     Sets the last error id.
        /// </summary>
        /// <param name="errorId"></param>
        void SetLast(string errorId);
    }
}