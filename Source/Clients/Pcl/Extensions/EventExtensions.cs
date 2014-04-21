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
        /// <summary>
        /// Add all of the default information to the error.  This information can be controlled from the server configuration
        /// values, application configuration, and any registered plugins.
        /// </summary>
        /// <param name="ev">The error model.</param>
        /// <param name="pluginContextData">
        /// Any contextual data objects to be used by Exceptionless plugins to gather default
        /// information for inclusion in the report information.
        /// </param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Default.
        /// </param>
        public static void AddDefaultInformation(this Event ev, IDictionary<string, object> pluginContextData = null, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Default;

            //            Configuration configuration = client.Configuration;
            //            if (configuration.IncludePrivateInformation)
            //                error.UserName = Environment.UserName;

            //            try {
            //                error.EnvironmentInfo = EnvironmentInfoCollector.Collect();
            //            } catch (Exception ex) {
            //                client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error adding machine information: {0}", ex.Message);
            //            }

            //#if !SILVERLIGHT
            //            try {
            //                if (configuration.TraceLogLimit > 0)
            //                    AddRecentTraceLogEntries(error);
            //            } catch (Exception ex) {
            //                client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error adding trace information: {0}", ex.Message);
            //            }
            //#endif

            //            foreach (string tag in client.Tags)
            //                error.Tags.Add(tag);

            //            foreach (IExceptionlessPlugin plugin in client.Plugins) {
            //                try {
            //                    plugin.AddDefaultInformation(new ExceptionlessPluginContext(client, pluginContextData), error);
            //                } catch (Exception ex) {
            //                    client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error adding default information: {0}", ex.Message);
            //                }
            //            }

            //#if !SILVERLIGHT
            //            ExceptionlessSection settings = ClientConfigurationReader.GetApplicationConfiguration(client);
            //            if (settings == null)
            //                return;

            //            foreach (NameValueConfigurationElement cf in settings.ExtendedData) {
            //                if (!String.IsNullOrEmpty(cf.Name))
            //                    error.ExtendedData[cf.Name] = cf.Value;
            //            }

            //            foreach (string tag in settings.Tags.SplitAndTrim(',')) {
            //                if (!String.IsNullOrEmpty(tag))
            //                    error.Tags.Add(tag);
            //            }
            //#endif
        }

        //#if !SILVERLIGHT
        ///// <summary>
        ///// Adds the trace info as extended data to the error.
        ///// </summary>
        ///// <param name="error">The error model.</param>
        //public static void AddRecentTraceLogEntries(this Error error) {
        //if (error.ExtendedData.ContainsKey(DataDictionary.TRACE_LOG_KEY))
        //    return;

        //ExceptionlessTraceListener traceListener = Trace.Listeners
        //    .OfType<ExceptionlessTraceListener>()
        //    .FirstOrDefault();

        //if (traceListener == null)
        //    return;

        //List<string> logEntries = traceListener.GetLogEntries();
        //if (logEntries.Count > 0)
        //    error.ExtendedData.Add(DataDictionary.TRACE_LOG_KEY, traceListener.GetLogEntries());
        //}
        //#endif

        public static Error GetError(this Event ev, IJsonSerializer serializer = null) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.Error))
                return null;

            try {
                return ev.Data.GetValue<Error>(Event.KnownDataKeys.Error, serializer);
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