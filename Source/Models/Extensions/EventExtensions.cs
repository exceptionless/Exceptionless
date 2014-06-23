#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless {
    public static class EventExtensions {
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
        public static UserInfo GetUserInfo(this Event ev) {
            object value;
            return ev.Data.TryGetValue(Event.KnownDataKeys.UserInfo, out value) ? value as UserInfo : null;
        }

        /// <summary>
        /// Adds the user info to the event.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="identity">A unique user identifier (E.G., email address, user name)</param>
        public static void AddUserInfo(this Event ev, string identity) {
            if (String.IsNullOrWhiteSpace(identity))
                return;

            var userInfo = ev.GetUserInfo() ?? new UserInfo(identity);
            userInfo.Identity = identity;

            ev.AddUserInfo(userInfo);
        }

        /// <summary>
        /// Adds the user info to the event.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="userInfo">The user info</param>
        public static void AddUserInfo(this Event ev, UserInfo userInfo) {
            if (userInfo == null)
                return;

            ev.Data[Event.KnownDataKeys.UserInfo] = userInfo;
        }

        /// <summary>
        /// Gets the user description from extended data.
        /// </summary>
        public static string GetUserDescription(this Event ev) {
            object value;
            return ev.Data.TryGetValue(Event.KnownDataKeys.UserDescription, out value) ? value as string : null;
        }

        /// <summary>
        /// Adds the user description to the event.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="description">The description</param>
        public static void AddUserDescription(this Event ev, string description) {
            if (String.IsNullOrWhiteSpace(description))
                return;

            ev.Data[Event.KnownDataKeys.UserDescription] = description;
        }
    }
}