using System;
using System.Collections.Generic;

namespace NLog.Fluent {
    public static class LogBuilderExtensions {
        public static LogBuilder Critical(this LogBuilder builder, bool isCritical = true) {
            return isCritical ? builder.Tag("Critical") : builder;
        }

        public static LogBuilder Tag(this LogBuilder builder, params string[] tags) {
            var tagList = builder.LogEventInfo.GetTags();
            tagList.AddRange(tags);

            return builder;
        }

        public static LogBuilder ContextProperty(this LogBuilder builder, string key, object value) {
            var contextData = builder.LogEventInfo.GetContextData();
            contextData[key] = value;

            return builder;
        }

        public static LogBuilder MarkUnhandled(this LogBuilder builder, string submissionMethod = null) {
            var contextData = builder.LogEventInfo.GetContextData();
            contextData.MarkAsUnhandledError();
            if (!String.IsNullOrEmpty(submissionMethod))
                contextData.SetSubmissionMethod(submissionMethod);

            return builder;
        }

        public static void MarkAsUnhandledError(this IDictionary<string, object> contextData) {
            contextData[IsUnhandledError] = true;
        }

        public static void SetSubmissionMethod(this IDictionary<string, object> contextData, string submissionMethod) {
            contextData[SubmissionMethod] = submissionMethod;
        }

        public static List<string> GetTags(this LogEventInfo ev) {
            var tagList = new List<string>();
            if (!ev.Properties.ContainsKey(Tags))
                ev.Properties[Tags] = tagList;

            if (ev.Properties.ContainsKey(Tags)
                && ev.Properties[Tags] is List<string>)
                tagList = (List<string>)ev.Properties[Tags];

            return tagList;
        }

        public static IDictionary<string, object> GetContextData(this LogEventInfo ev) {
            IDictionary<string, object> contextData = new Dictionary<string, object>();
            if (!ev.Properties.ContainsKey(ContextData))
                ev.Properties[ContextData] = contextData;

            if (ev.Properties.ContainsKey(ContextData)
                && ev.Properties[ContextData] is IDictionary<string, object>)
                contextData = (IDictionary<string, object>)ev.Properties[ContextData];

            return contextData;
        }

        private const string IsUnhandledError = "@@_IsUnhandledError";
        private const string SubmissionMethod = "@@_SubmissionMethod";
        private const string Tags = "Tags";
        private const string ContextData = "ContextData";


        public static LogBuilder Project(this LogBuilder builder, string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return builder;

            return builder.Property("project", projectId);
        }

        public static LogBuilder Organization(this LogBuilder builder, string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return builder;

            return builder.Property("organization", organizationId);
        }
    }
}