#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.ExtendedData;
using Exceptionless.Models;
using Exceptionless.Nancy;
using Nancy;
using Nancy.Bootstrapper;

namespace Exceptionless {
    public static class ExceptionlessNancyExtensions {
        private const string NANCY_CONTEXT= "NancyContext";

        public static void RegisterNancy(this ExceptionlessClient client, IPipelines pipelines) {
            client.RegisterPlugin(new ExceptionlessNancyPlugin());
            client.Startup();
            client.Configuration.IncludePrivateInformation = true;

            pipelines.OnError += OnError;
        }

        private static Response OnError(NancyContext context, Exception exception) {
            var contextData = new Dictionary<string, object> { { NANCY_CONTEXT, context } };

            ExceptionlessClient.Current.ProcessUnhandledException(exception, "UnhandledNancyPipelineException", true, contextData);

            return context.Response;
        }

        public static void UnregisterNancy(this ExceptionlessClient client) {
            client.UnregisterPlugin(typeof(ExceptionlessNancyPlugin).FullName);
            client.Shutdown();
        }

        public static Error AddRequestInfo(this Error error, NancyContext context) {
            if (context == null)
                return error;
 
            error.RequestInfo = NancyRequestInfoCollector.Collect(context, ExceptionlessClient.Current);
 
            return error;
        }

        public static ErrorBuilder AddRequestInfo(this ErrorBuilder builder, NancyContext context) {
            builder.Target.AddRequestInfo(context);
            return builder;
        }

        internal static NancyContext GetNancyContext(this IDictionary<string, object> data) {
            if (!data.HasNancyContext())
                return null;

            return data[NANCY_CONTEXT] as NancyContext;
        }

        internal static bool HasNancyContext(this IDictionary<string, object> data) {
            return data.ContainsKey(NANCY_CONTEXT);
        }
    }
}
