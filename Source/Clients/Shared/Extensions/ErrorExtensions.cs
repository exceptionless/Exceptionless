#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Configuration;
using Exceptionless.Diagnostics;
using Exceptionless.ExtendedData;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Plugins;
using Exceptionless.Serialization;
using ClientConfiguration = Exceptionless.Configuration.ClientConfiguration;

namespace Exceptionless {
    public static class ErrorExtensions {
        /// <summary>
        /// Creates a builder object for constructing error reports in a fluent api. Automatically includes all default report
        /// information.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns></returns>
        public static ErrorBuilder ToExceptionless(this Exception exception) {
            return ToExceptionless(exception, true);
        }

        /// <summary>
        /// Creates a builder object for constructing error reports in a fluent api.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <param name="addDefaultInformation">Wether the default information should be included in the report.</param>
        /// <param name="pluginContextData">
        /// Any contextual data objects to be used by Exceptionless plugins to gather default
        /// information for inclusion in the report information.
        /// </param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Current.
        /// </param>
        /// <returns></returns>
        public static ErrorBuilder ToExceptionless(this Exception exception, bool addDefaultInformation, IDictionary<string, object> pluginContextData = null, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Current;
            var builder = new ErrorBuilder(ExceptionlessClient.ToError(client, exception));
            return addDefaultInformation ? builder.AddDefaultInformation(pluginContextData) : builder;
        }

        /// <summary>
        /// Add all of the default information to the error.  This information can be controlled from the server configuration
        /// values, application configuration, and any registered plugins.
        /// </summary>
        /// <param name="error">The error model.</param>
        /// <param name="pluginContextData">
        /// Any contextual data objects to be used by Exceptionless plugins to gather default
        /// information for inclusion in the report information.
        /// </param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Current.
        /// </param>
        public static void AddDefaultInformation(this Error error, IDictionary<string, object> pluginContextData = null, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Current;

            ClientConfiguration configuration = client.Configuration;

            if (client.Configuration.IncludePrivateInformation)
                error.UserName = Environment.UserName;

            try {
                error.EnvironmentInfo = EnvironmentInfoCollector.Collect();
            } catch (Exception ex) {
                client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error adding machine information: {0}", ex.Message);
            }

#if !SILVERLIGHT
            try {
                if (configuration.TraceLogLimit > 0)
                    AddRecentTraceLogEntries(error);
            } catch (Exception ex) {
                client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error adding trace information: {0}", ex.Message);
            }
#endif

            foreach (string tag in client.Tags)
                error.Tags.Add(tag);

            foreach (IExceptionlessPlugin plugin in client.Plugins) {
                try {
                    plugin.AddDefaultInformation(new ExceptionlessPluginContext(client, pluginContextData), error);
                } catch (Exception ex) {
                    client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error adding default information: {0}", ex.Message);
                }
            }

#if !SILVERLIGHT
            ExceptionlessSection settings = ClientConfigurationReader.GetApplicationConfiguration(client);
            if (settings == null)
                return;

            foreach (NameValueConfigurationElement cf in settings.ExtendedData) {
                if (!String.IsNullOrEmpty(cf.Name))
                    error.ExtendedData[cf.Name] = cf.Value;
            }

            foreach (string tag in settings.Tags.SplitAndTrim(',')) {
                if (!String.IsNullOrEmpty(tag))
                    error.Tags.Add(tag);
            }
#endif
        }

#if !SILVERLIGHT
        /// <summary>
        /// Adds the trace info as extended data to the error.
        /// </summary>
        /// <param name="error">The error model.</param>
        public static void AddRecentTraceLogEntries(this Error error) {
            if (error.ExtendedData.ContainsKey(DataDictionary.TRACE_LOG_KEY))
                return;

            ExceptionlessTraceListener traceListener = Trace.Listeners
                .OfType<ExceptionlessTraceListener>()
                .FirstOrDefault();

            if (traceListener == null)
                return;

            List<string> logEntries = traceListener.GetLogEntries();
            if (logEntries.Count > 0)
                error.ExtendedData.Add(DataDictionary.TRACE_LOG_KEY, traceListener.GetLogEntries());
        }
#endif

        /// <summary>
        /// Adds the object to extended data.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <param name="data">The data object to add.</param>
        /// <param name="name">The name of the object to add.</param>
        /// <param name="maxDepth">The max depth of the object to include.</param>
        /// <param name="excludedPropertyNames">Any property names that should be excluded</param>
        /// <param name="ignoreSerializationErrors">Specifies wether properties that throw errors while serializing be ignored</param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Current.
        /// </param>
        public static void AddObject(this Error error, object data, string name = null, int? maxDepth = null, ICollection<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Current;

            if (data == null)
                return;

            ExtendedDataInfo info;
            if (data is ExtendedDataInfo)
                info = data as ExtendedDataInfo;
            else {
                info = new ExtendedDataInfo {
                    Data = data,
                    Name = name,
                    MaxDepthToSerialize = maxDepth,
                    ExcludedPropertyNames = excludedPropertyNames != null ? client.Configuration.DataExclusions.Union(excludedPropertyNames).ToArray() : client.Configuration.DataExclusions,
                    IgnoreSerializationErrors = ignoreSerializationErrors
                };
            }

            AddObject(error, info);
        }

        /// <summary>
        /// Adds the object to extended data.
        /// </summary>
        /// <param name="error">The error to add the object to.</param>
        /// <param name="info">The data object to add.</param>
        /// <param name="client">
        /// The ExceptionlessClient instance used for configuration. If a client is not specified, it will use
        /// ExceptionlessClient.Current.
        /// </param>
        public static void AddObject(this Error error, ExtendedDataInfo info, ExceptionlessClient client = null) {
            if (client == null)
                client = ExceptionlessClient.Current;

            if (info == null || info.Data == null)
                return;

            string name = info.Data.GetType().Name;

            if (!String.IsNullOrEmpty(info.Name))
                name = info.Name;

            string json = String.Empty;

            ICollection<string> excludedPropertyNames = info.ExcludedPropertyNames != null ? client.Configuration.DataExclusions.Union(info.ExcludedPropertyNames).ToArray() : client.Configuration.DataExclusions;

            try {
                json = ModelSerializer.Current.SerializeToString(info.Data, info.MaxDepthToSerialize, excludedPropertyNames, info.IgnoreSerializationErrors);
            } catch (Exception ex) {
                json = ex.ToString();
            }

            if (error.ExtendedData.ContainsKey(name))
                error.ExtendedData[name] = json;
            else
                error.ExtendedData.Add(name, json);
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