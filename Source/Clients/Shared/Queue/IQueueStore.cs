#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    internal interface IQueueStore {
        /// <summary>
        /// Verifies that the store provider can be created and used.
        /// </summary>
        /// <returns></returns>
        bool VerifyStoreIsUsable();

        /// <summary>
        /// Enqueue a new error report.
        /// </summary>
        /// <param name="error"></param>
        void Enqueue(Error error);

        /// <summary>
        /// Updates the manifest for an error report.
        /// </summary>
        /// <param name="manifest"></param>
        void UpdateManifest(Manifest manifest);

        /// <summary>
        /// Deletes the manifest and related files for the given error id.
        /// </summary>
        void Delete(string id);

        /// <summary>
        /// Deletes all files older than the specified date.
        /// </summary>
        int Cleanup(DateTime target);

        /// <summary>
        /// Gets the error object for a given error id.
        /// </summary>
        /// <returns>The error object.</returns>
        Error GetError(string id);

        /// <summary>
        /// The log accessor used to get a logging instance.
        /// </summary>
        IExceptionlessLogAccessor LogAccessor { get; }

        /// <summary>
        /// Gets the manifests.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Manifest> GetManifests(int? limit, bool includePostponed = true, DateTime? manifestsLastWriteTimeOlderThan = null);
    }
}