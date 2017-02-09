using System;
using System.Collections.Generic;
using System.Linq;

namespace Foundatio.Logging {
    public static class LogBuilderExtensions {
        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public static ILogBuilder Critical(this ILogBuilder builder, bool isCritical = true) {
            return isCritical ? builder.Tag("Critical") : builder;
        }

        /// <summary>
        /// Adds one or more tags to the event.
        /// </summary>
        /// <param name="builder">The log builder object.</param>
        /// <param name="tags">The tags to be added to the event.</param>
        public static ILogBuilder Tag(this ILogBuilder builder, params string[] tags) {
            if (builder.LogData == null)
                return builder;

            if (builder.LogData.Properties == null)
                builder.LogData.Properties = new Dictionary<string, object>();

            var tagList = new List<string>();
            if (builder.LogData.Properties.ContainsKey(Tags) && builder.LogData.Properties[Tags] is List<string>)
                tagList = builder.LogData.Properties[Tags] as List<string>;

            foreach (string tag in tags) {
                if (!tagList.Any(s => s.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                    tagList.Add(tag);
            }

            return builder.Property(Tags, tagList);
        }

        public static ILogBuilder Value(this ILogBuilder builder, decimal value) {
            return builder.Property("@value", value);
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) that the event happened to.
        /// </summary>
        /// <param name="builder">The log builder object.</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        public static ILogBuilder Identity(this ILogBuilder builder, string identity) {
            return builder.Identity(identity, null);
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) that the event happened to.
        /// </summary>
        /// <param name="builder">The log builder object.</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        /// <param name="name">The user's friendly name that the event happened to.</param>
        public static ILogBuilder Identity(this ILogBuilder builder, string identity, string name) {
            if (String.IsNullOrWhiteSpace(identity) && String.IsNullOrWhiteSpace(name))
                return builder;

            return builder.Property("@user", new { Identity = identity, Name = name });
        }

        public static ILogBuilder ContextProperty(this ILogBuilder builder, string key, object value) {
            var contextData = builder.GetContextData();
            if (contextData != null)
                contextData[key] = value;

            return builder;
        }
        
        /// <summary>
        /// Marks the event as being a unhandled occurrence and sets the submission method.
        /// </summary>
        /// <param name="builder">The log builder object.</param>
        /// <param name="submissionMethod">The submission method.</param>
        public static ILogBuilder MarkUnhandled(this ILogBuilder builder, string submissionMethod = null) {
            var contextData = builder.GetContextData();
            if (contextData == null)
                return builder;
            
            contextData.MarkAsUnhandledError();
            if (!String.IsNullOrEmpty(submissionMethod))
                contextData.SetSubmissionMethod(submissionMethod);

            return builder;
        }

        /// <summary>
        /// Marks the event as being a unhandled error occurrence.
        /// </summary>
        public static void MarkAsUnhandledError(this IDictionary<string, object> contextData) {
            contextData[IsUnhandledError] = true;
        }

        /// <summary>
        /// Sets the submission method that created the event (E.G., UnobservedTaskException)
        /// </summary>
        public static void SetSubmissionMethod(this IDictionary<string, object> contextData, string submissionMethod) {
            contextData[SubmissionMethod] = submissionMethod;
        }

        private static IDictionary<string, object> GetContextData(this ILogBuilder builder) {
            if (builder.LogData == null)
                return null;

            if (builder.LogData.Properties == null)
                builder.LogData.Properties = new Dictionary<string, object>();
            
            IDictionary<string, object> contextData = new Dictionary<string, object>();
            if (!builder.LogData.Properties.ContainsKey(ContextData))
                builder.LogData.Properties[ContextData] = contextData;

            if (builder.LogData.Properties.ContainsKey(ContextData)
                && builder.LogData.Properties[ContextData] is IDictionary<string, object>)
                contextData = (IDictionary<string, object>)builder.LogData.Properties[ContextData];

            return contextData;
        }

        private const string IsUnhandledError = "@@_IsUnhandledError";
        private const string SubmissionMethod = "@@_SubmissionMethod";
        private const string Tags = "Tags";
        private const string ContextData = "ContextData";

        public static ILogBuilder Project(this ILogBuilder builder, string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return builder;

            return builder.Property("project", projectId);
        }

        public static ILogBuilder Organization(this ILogBuilder builder, string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return builder;

            return builder.Property("organization", organizationId);
        }
    }
}