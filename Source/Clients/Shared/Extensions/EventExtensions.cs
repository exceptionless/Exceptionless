#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless {
    public static class EventExtensions {
        public static Error GetError(this Event ev, IJsonSerializer serializer = null) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.Error))
                return null;

            try {
                return ev.Data.GetValue<Error>(Event.KnownDataKeys.Error, serializer);
            } catch (Exception) {}

            return null;
        }

        public static SimpleError GetSimpleError(this Event ev, IJsonSerializer serializer = null) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError))
                return null;

            try {
                return ev.Data.GetValue<SimpleError>(Event.KnownDataKeys.SimpleError, serializer);
            } catch (Exception) {}

            return null;
        }

        public static RequestInfo GetRequestInfo(this Event ev, IJsonSerializer serializer = null) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.RequestInfo))
                return null;

            try {
                return ev.Data.GetValue<RequestInfo>(Event.KnownDataKeys.RequestInfo, serializer);
            } catch (Exception) {}

            return null;
        }

        public static EnvironmentInfo GetEnvironmentInfo(this Event ev, IJsonSerializer serializer = null) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.EnvironmentInfo))
                return null;

            try {
                return ev.Data.GetValue<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo, serializer);
            } catch (Exception) {}

            return null;
        }
    }

    /// <summary>
    /// A class that contains info about objects that will be added to the error report's ExtendedData collection.
    /// </summary>
    public class ExtendedDataInfo {
        public ExtendedDataInfo() {
            ExcludedPropertyNames = new List<string>();
        }

        /// <summary>
        /// The name to use for the ExtendedData entry.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The object that should be serialized and added to the ExtendedData of the error.
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// The maximum depth to go into the object graph when serializing the data.
        /// </summary>
        public int? MaxDepthToSerialize { get; set; }

        /// <summary>
        /// The names of any properties that should be excluded.
        /// </summary>
        public ICollection<string> ExcludedPropertyNames { get; set; }

        /// <summary>
        /// Specifies if properties that throw serialization errors should be ignored.
        /// </summary>
        public bool IgnoreSerializationErrors { get; set; }
    }
}