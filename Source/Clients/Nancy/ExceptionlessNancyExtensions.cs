#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Enrichments;
using Exceptionless.ExtendedData;
using Exceptionless.Models;
using Exceptionless.Nancy;
using Nancy;
using Nancy.Bootstrapper;

namespace Exceptionless {
    public static class ExceptionlessNancyExtensions {
        private const string NANCY_CONTEXT = "NancyContext";

        public static void RegisterNancy(this ExceptionlessClient client, IPipelines pipelines) {
            client.Startup();
            client.Configuration.IncludePrivateInformation = true;
            client.Configuration.AddEnrichment<ExceptionlessNancyEnrichment>();

            pipelines.OnError += OnError;
            pipelines.AfterRequest += AfterRequest;
        }

        private static Response OnError(NancyContext context, Exception exception) {
            var contextData = new ContextData();
            contextData.SetUnhandled();
            contextData.SetSubmissionMethod("NancyPipelineException");
            contextData.Add(NANCY_CONTEXT, context);

            exception.ToExceptionless(contextData).Submit();

            return context.Response;
        }

        private static void AfterRequest(NancyContext context) {
            // TODO: We need to be using the pass in the registered exceptionless client.
            var contextData = new ContextData { { NANCY_CONTEXT, context } };
            if (context.Response.StatusCode == HttpStatusCode.NotFound)
                ExceptionlessClient.Default.SubmitEvent(new Event { Type = Event.KnownTypes.NotFound }, contextData);
        }

        public static void UnregisterNancy(this ExceptionlessClient client) {
            client.Shutdown();
            client.Configuration.RemoveEnrichment<ExceptionlessNancyEnrichment>();
        }

        public static Event AddRequestInfo(this Event ev, NancyContext context) {
            if (context == null)
                return ev;

            ev.AddRequestInfo(NancyRequestInfoCollector.Collect(context, ExceptionlessClient.Default.Configuration.DataExclusions));

            return ev;
        }

        /// <summary>
        /// Adds the current request info as extended data to the event.
        /// </summary>
        /// <param name="builder">The event builder.</param>
        /// <param name="context">The nancy context to gather information from.</param>
        public static EventBuilder AddRequestInfo(this EventBuilder builder, NancyContext context) {
            builder.Target.AddRequestInfo(context);
            return builder;
        }

        internal static NancyContext GetNancyContext(this IDictionary<string, object> data) {
            if (!data.ContainsKey(NANCY_CONTEXT))
                return null;

            return data[NANCY_CONTEXT] as NancyContext;
        }
    }
}