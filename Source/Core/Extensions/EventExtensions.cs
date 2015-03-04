using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless {
    public static class EventExtensions {
        public static Error GetError(this Event ev) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.Error))
                return null;

            try {
                return ev.Data.GetValue<Error>(Event.KnownDataKeys.Error);
            } catch (Exception) {}

            return null;
        }

        public static SimpleError GetSimpleError(this Event ev) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError))
                return null;

            try {
                return ev.Data.GetValue<SimpleError>(Event.KnownDataKeys.SimpleError);
            } catch (Exception) {}

            return null;
        }

        public static RequestInfo GetRequestInfo(this Event ev) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.RequestInfo))
                return null;

            try {
                return ev.Data.GetValue<RequestInfo>(Event.KnownDataKeys.RequestInfo);
            } catch (Exception) {}

            return null;
        }

        public static EnvironmentInfo GetEnvironmentInfo(this Event ev) {
            if (ev == null || !ev.Data.ContainsKey(Event.KnownDataKeys.EnvironmentInfo))
                return null;

            try {
                return ev.Data.GetValue<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo);
            } catch (Exception) {}

            return null;
        }

        /// <summary>
        /// Indicates wether the event has been marked as critical.
        /// </summary>
        public static bool IsCritical(this Event ev) {
            return ev.Tags != null && ev.Tags.Contains(Event.KnownTags.Critical);
        }

        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public static void MarkAsCritical(this Event ev) {
            if (ev.Tags == null)
                ev.Tags = new TagSet();

            ev.Tags.Add(Event.KnownTags.Critical);
        }

        /// <summary>
        /// Returns true if the event type is not found.
        /// </summary>
        public static bool IsNotFound(this Event ev) {
            return ev.Type == Event.KnownTypes.NotFound;
        }

        /// <summary>
        /// Returns true if the event type is error.
        /// </summary>
        public static bool IsError(this Event ev) {
            return ev.Type == Event.KnownTypes.Error;
        }

        /// <summary>
        /// Returns true if the event type is log.
        /// </summary>
        public static bool IsLog(this Event ev) {
            return ev.Type == Event.KnownTypes.Log;
        }

        /// <summary>
        /// Returns true if the event type is feature usage.
        /// </summary>
        public static bool IsFeatureUsage(this Event ev) {
            return ev.Type == Event.KnownTypes.FeatureUsage;
        }

        /// <summary>
        /// Returns true if the event type is session start.
        /// </summary>
        public static bool IsSessionStart(this Event ev) {
            return ev.Type == Event.KnownTypes.SessionStart;
        }

        /// <summary>
        /// Returns true if the event type is session end.
        /// </summary>
        public static bool IsSessionEnd(this Event ev) {
            return ev.Type == Event.KnownTypes.SessionEnd;
        }

        /// <summary>
        /// Adds the request info to the event.
        /// </summary>
        public static void AddRequestInfo(this Event ev, RequestInfo request) {
            if (request == null)
                return;

            ev.Data[Event.KnownDataKeys.RequestInfo] = request;
        }

        /// <summary>
        /// Gets the user info object from extended data.
        /// </summary>
        public static UserInfo GetUserIdentity(this Event ev) {
            object value;
            return ev.Data.TryGetValue(Event.KnownDataKeys.UserInfo, out value) ? value as UserInfo : null;
        }

        /// <summary>
        /// Sets the version that the event happened on.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="version">The version.</param>
        public static void SetVersion(this Event ev, string version) {
            if (String.IsNullOrWhiteSpace(version))
                return;

            ev.Data[Event.KnownDataKeys.Version] = version.Trim();
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) that the event happened to.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        public static void SetUserIdentity(this Event ev, string identity) {
            ev.SetUserIdentity(identity, null);
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) and name that the event happened to.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        /// <param name="name">The user's friendly name that the event happened to.</param>
        public static void SetUserIdentity(this Event ev, string identity, string name) {
            if (String.IsNullOrWhiteSpace(identity) && String.IsNullOrWhiteSpace(name))
                return;

            ev.SetUserIdentity(new UserInfo(identity, name));
        }

        /// <summary>
        /// Sets the user's identity (ie. email address, username, user id) and name that the event happened to.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="userInfo">The user's identity that the event happened to.</param>
        public static void SetUserIdentity(this Event ev, UserInfo userInfo) {
            if (userInfo == null)
                return;

            ev.Data[Event.KnownDataKeys.UserInfo] = userInfo;
        }

        /// <summary>
        /// Gets the user description from extended data.
        /// </summary>
        public static UserDescription GetUserDescription(this Event ev) {
            object value;
            return ev.Data.TryGetValue(Event.KnownDataKeys.UserDescription, out value) ? value as UserDescription : null;
        }

        /// <summary>
        /// Sets the user's description of the event.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="emailAddress">The email address</param>
        /// <param name="description">The user's description of the event.</param>
        public static void SetUserDescription(this Event ev, string emailAddress, string description) {
            if (String.IsNullOrWhiteSpace(emailAddress) && String.IsNullOrWhiteSpace(description))
                return;

            ev.Data[Event.KnownDataKeys.UserDescription] = new UserDescription(emailAddress, description);
        }

        /// <summary>
        /// Sets the user's description of the event.
        /// </summary>
        /// <param name="ev">The event.</param>
        /// <param name="description">The user's description.</param>
        public static void SetUserDescription(this Event ev, UserDescription description) {
            if (description == null || (String.IsNullOrWhiteSpace(description.EmailAddress) && String.IsNullOrWhiteSpace(description.Description)))
                return;

            ev.Data[Event.KnownDataKeys.UserDescription] = description;
        }
    }
}