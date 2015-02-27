#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Enrichments;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless {
    public class EventBuilder {
        public EventBuilder(Event ev, ExceptionlessClient client = null, ContextData enrichmentContextData = null) {
            Client = client ?? ExceptionlessClient.Default;
            Target = ev;
            EnrichmentContextData = enrichmentContextData;
        }

        /// <summary>
        ///     Any contextual data objects to be used by Exceptionless enrichments to gather additional
        ///     information for inclusion in the event.
        /// </summary>
        public ContextData EnrichmentContextData { get; private set; }

        public ExceptionlessClient Client { get; set; }
        
        public Event Target { get; private set; }

        /// <summary>
        /// Sets the event type.
        /// </summary>
        /// <param name="type">The event type.</param>
        public EventBuilder SetType(string type) {
            Target.Type = type;
            return this;
        }

        /// <summary>
        /// Sets the event source.
        /// </summary>
        /// <param name="source">The event source.</param>
        public EventBuilder SetSource(string source) {
            Target.Source = source;
            return this;
        }

        /// <summary>
        /// Sets the event session id.
        /// </summary>
        /// <param name="sessionId">The event session id.</param>
        public EventBuilder SetSessionId(string sessionId) {
            if (!IsValidIdentifier(sessionId))
                throw new ArgumentException("SessionId must contain between 8 and 100 alphanumeric or '-' characters.", "sessionId");

            Target.SessionId = sessionId;
            return this;
        }

        /// <summary>
        /// Sets the event reference id.
        /// </summary>
        /// <param name="referenceId">The event reference id.</param>
        public EventBuilder SetReferenceId(string referenceId) {
            if (!IsValidIdentifier(referenceId))
                throw new ArgumentException("ReferenceId must contain between 8 and 100 alphanumeric or '-' characters.", "referenceId");

            Target.ReferenceId = referenceId;
            return this;
        }

        private bool IsValidIdentifier(string value) {
            if (value == null)
                return true;

            if (value.Length < 8 || value.Length > 100)
                return false;

            return value.IsValidIdentifier();
        }

        /// <summary>
        /// Sets the event message.
        /// </summary>
        /// <param name="message">The event message.</param>
        public EventBuilder SetMessage(string message) {
            Target.Message = message;
            return this;
        }

        /// <summary>
        /// Sets the event geo coordinates. Can be either "lat,lon" or an IP address that will be used to auto detect the geo coordinates.
        /// </summary>
        /// <param name="coordinates">The event coordinates.</param>
        public EventBuilder SetGeo(string coordinates) {
            if (String.IsNullOrWhiteSpace(coordinates)) {
                Target.Geo = null;
                return this;
            }

            if (coordinates.Contains(",") || coordinates.Contains(".") || coordinates.Contains(":"))
                Target.Geo = coordinates;
            else
                throw new ArgumentException("Must be either lat,lon or an IP address.", "coordinates");

            return this;
        }

        /// <summary>
        /// Sets the event geo coordinates.
        /// </summary>
        /// <param name="latitude">The event latitude.</param>
        /// <param name="longitude">The event longitude.</param>
        public EventBuilder SetGeo(double latitude, double longitude) {
            if (latitude < -90.0 || latitude > 90.0)
                throw new ArgumentOutOfRangeException("latitude", "Must be a valid latitude value between -90.0 and 90.0.");
            if (longitude < -180.0 || longitude > 180.0)
                throw new ArgumentOutOfRangeException("longitude", "Must be a valid longitude value between -180.0 and 180.0.");
            
            Target.Geo = latitude + "," + longitude;
            return this;
        }

        /// <summary>
        /// Sets the event value.
        /// </summary>
        /// <param name="value">The value of the event.</param>
        public EventBuilder SetValue(decimal value) {
            Target.Value = value;
            return this;
        }

        /// <summary>
        ///     Adds one or more tags to the event.
        /// </summary>
        /// <param name="tags">The tags to be added to the event.</param>
        public EventBuilder AddTags(params string[] tags) {
            if (tags == null || tags.Length == 0)
                return this;

            Target.Tags.AddRange(tags.Where(t => !String.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
            return this;
        }

        /// <summary>
        ///     Sets an extended property value to include with the event. Use either <paramref name="excludedPropertyNames" /> or
        ///     <see cref="Exceptionless.Json.JsonIgnoreAttribute" /> to exclude data from being included in the event report.
        /// </summary>
        /// <param name="name">The name of the object to add.</param>
        /// <param name="value">The data object to add.</param>
        /// <param name="maxDepth">The max depth of the object to include.</param>
        /// <param name="excludedPropertyNames">Any property names that should be excluded.</param>
        /// <param name="ignoreSerializationErrors">Specifies if properties that throw serialization errors should be ignored.</param>
        public EventBuilder SetProperty(string name, object value, int? maxDepth = null, ICollection<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false) {
            if (value != null)
                Target.AddObject(value, name, maxDepth, excludedPropertyNames, ignoreSerializationErrors);

            return this;
        }

        /// <summary>
        ///     Adds the object to extended data. Use either <paramref name="excludedPropertyNames" /> or
        ///     <see cref="Exceptionless.Json.JsonIgnoreAttribute" /> to exclude data from being included in the event.
        /// </summary>
        /// <param name="data">The data object to add.</param>
        /// <param name="name">The name of the object to add.</param>
        /// <param name="maxDepth">The max depth of the object to include.</param>
        /// <param name="excludedPropertyNames">Any property names that should be excluded.</param>
        /// <param name="ignoreSerializationErrors">Specifies if properties that throw serialization errors should be ignored.</param>
        public EventBuilder AddObject(object data, string name = null, int? maxDepth = null, ICollection<string> excludedPropertyNames = null, bool ignoreSerializationErrors = false) {
            if (data != null)
                Target.AddObject(data, name, maxDepth, excludedPropertyNames, ignoreSerializationErrors);
            
            return this;
        }

        /// <summary>
        ///     Marks the event as being a critical occurrence.
        /// </summary>
        public EventBuilder MarkAsCritical() {
            Target.MarkAsCritical();
            return this;
        }

        /// <summary>
        ///     Submits the event report.
        /// </summary>
        public void Submit() {
            Client.SubmitEvent(Target, EnrichmentContextData);
        }
    }
}