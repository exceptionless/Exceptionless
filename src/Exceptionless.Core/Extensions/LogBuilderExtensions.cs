using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.Logging {
    public class ExceptionlessState : Dictionary<string, object> {
        public ExceptionlessState() {}

        public ExceptionlessState Project(string projectId) {
            if (!String.IsNullOrEmpty(projectId))
                base["project"] = projectId;

            return this;
        }

        public ExceptionlessState Organization(string organizationId) {
            if (!String.IsNullOrEmpty(organizationId))
                base["organization"] = organizationId;

            return this;
        }

        /// <summary>
        /// Adds one or more tags to the event.
        /// </summary>
        /// <param name="tag">The tag to be added to the event.</param>
        public ExceptionlessState Tag(string tag) {
            if (String.IsNullOrEmpty(tag))
                return this;

            HashSet<string> tagList = null;
            if (TryGetValue(Tags, out var v) && v is HashSet<string> t)
                tagList = t;

            if (tagList == null)
                tagList = new HashSet<string>();

            tagList.Add(tag);
            base[Tags] = tagList;
            return this;
        }

        public ExceptionlessState Value(decimal value) {
            base["@value"] = value;
            return this;
        }

        public ExceptionlessState ManualStackingKey(string stackingKey) {
            if (!String.IsNullOrEmpty(stackingKey))
                base["@stack"] = stackingKey;

            return this;
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) that the event happened to.
        /// </summary>
        /// <param name="identity">The user's identity that the event happened to.</param>
        public ExceptionlessState Identity(string identity) {
            return Identity(identity, null);
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) that the event happened to.
        /// </summary>
        /// <param name="identity">The user's identity that the event happened to.</param>
        /// <param name="name">The user's friendly name that the event happened to.</param>
        public ExceptionlessState Identity(string identity, string name) {
            if (String.IsNullOrWhiteSpace(identity) && String.IsNullOrWhiteSpace(name))
                return this;

            base["@user"] = new { Identity = identity, Name = name };
            return this;
        }

        public ExceptionlessState Property(string key, object value) {
            base[key] = value;
            return this;
        }

        /// <summary>
        /// Marks the event as being a unhandled occurrence and sets the submission method.
        /// </summary>
        /// <param name="submissionMethod">The submission method.</param>
        public ExceptionlessState MarkUnhandled(string submissionMethod = null) {
            return MarkAsUnhandledError().SetSubmissionMethod(submissionMethod);
        }

        /// <summary>
        /// Marks the event as being a unhandled error occurrence.
        /// </summary>
        public ExceptionlessState MarkAsUnhandledError() {
            base[IsUnhandledError] = true;
            return this;
        }

        /// <summary>
        /// Sets the submission method that created the event (E.G., UnobservedTaskException)
        /// </summary>
        public ExceptionlessState SetSubmissionMethod(string submissionMethod) {
            base[SubmissionMethod] = submissionMethod;
            return this;
        }

        private const string IsUnhandledError = "@@_IsUnhandledError";
        private const string SubmissionMethod = "@@_SubmissionMethod";
        private const string Tags = "Tags";
    }
}