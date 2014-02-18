#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless {
    public class ErrorBuilder {
        public ErrorBuilder(Error error, ExceptionlessClient client = null) {
            Client = client ?? ExceptionlessClient.Default;
            Target = error;
        }

        public ExceptionlessClient Client { get; set; }
        public Error Target { get; private set; }

        /// <summary>
        ///     Add all of the default information to the error.  This information can be controlled from the server configuration
        ///     values, application configuration, and any registered plugins.
        /// </summary>
        /// <param name="contextData">Any contextual data objects to be used in gathering the default information.</param>
        public ErrorBuilder AddDefaultInformation(IDictionary<string, object> contextData = null) {
            Target.AddDefaultInformation(contextData);
            return this;
        }

        /// <summary>
        ///     Sets the user's email address that the error happened to.
        /// </summary>
        /// <param name="emailAddress">The user's email adddress that the error happened to.</param>
        public ErrorBuilder SetUserEmail(string emailAddress) {
            Target.UserEmail = emailAddress;
            return this;
        }

        /// <summary>
        ///     Sets the user's name that the error happened to.
        /// </summary>
        /// <param name="userName">The user's name that the error happened to.</param>
        public ErrorBuilder SetUserName(string userName) {
            Target.UserName = userName;
            return this;
        }

        /// <summary>
        ///     Sets the user's description of the error.
        /// </summary>
        /// <param name="description">The user's name description of the error.</param>
        public ErrorBuilder SetUserDescription(string description) {
            Target.UserDescription = description;
            return this;
        }

        /// <summary>
        ///     Adds one or more tags to the error.
        /// </summary>
        /// <param name="tags">The tags to be added to the error.</param>
        public ErrorBuilder AddTags(params string[] tags) {
            if (tags == null)
                return this;

            Target.Tags.AddRange(tags);
            return this;
        }

        /// <summary>
        ///     Adds the object to extended data. Use either <paramref name="excludedPropertyNames" /> or
        ///     <see cref="Exceptionless.Json.JsonIgnoreAttribute" /> to exclude data from being included in the error report.
        /// </summary>
        /// <param name="data">The data object to add.</param>
        /// <param name="name">The name of the object to add.</param>
        /// <param name="maxDepth">The max depth of the object to include.</param>
        /// <param name="excludedPropertyNames">Any property names that should be excluded.</param>
        /// <param name="ignoreSerializationErrors">Specifies if properties that throw serialization errors should be ignored.</param>
        public ErrorBuilder AddObject(object data, string name = null, int? maxDepth = null, ICollection<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false) {
            Target.AddObject(data, name, maxDepth, excludedPropertyNames, ignoreSerializationErrors);
            return this;
        }

        /// <summary>
        ///     Marks the error as being a critical occurrence.
        /// </summary>
        public ErrorBuilder MarkAsCritical() {
            Target.MarkAsCritical();
            return this;
        }

        /// <summary>
        ///     Submits the error report.
        /// </summary>
        public void Submit() {
            Client.SubmitError(Target);
        }
    }
}