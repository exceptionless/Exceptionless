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
        private const string NANCY_CONTEXT = "NancyContext";

        public static void RegisterNancy(this ExceptionlessClient client, IPipelines pipelines) {
            client.RegisterPlugin(new ExceptionlessNancyPlugin());
            client.Startup();
            client.Configuration.IncludePrivateInformation = true;

            pipelines.OnError += OnError;
            pipelines.AfterRequest += AfterRequest;
        }

        private static Response OnError(NancyContext context, Exception exception) {
            var contextData = new Dictionary<string, object> { { NANCY_CONTEXT, context } };

            ExceptionlessClient.Default.ProcessUnhandledException(exception, "NancyPipelineException", true, contextData);

            return context.Response;
        }

        private static void AfterRequest(NancyContext context) {
            var contextData = new Dictionary<string, object> { { NANCY_CONTEXT, context } };

            if (context.Response.StatusCode == HttpStatusCode.NotFound)
                new NotFoundException().ToExceptionless(true, contextData).Submit();
        }

        public static void UnregisterNancy(this ExceptionlessClient client) {
            client.UnregisterPlugin(typeof(ExceptionlessNancyPlugin).FullName);
            client.Shutdown();
        }

        public static Event AddRequestInfo(this Event ev, NancyContext context) {
            if (context == null)
                return ev;

            ev.RequestInfo = NancyRequestInfoCollector.Collect(context, ExceptionlessClient.Default);

            return ev;
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