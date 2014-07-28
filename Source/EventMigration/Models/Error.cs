#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;
using ModuleCollection = Exceptionless.EventMigration.Models.Collections.ModuleCollection;

namespace Exceptionless.EventMigration.Models {
    public class Error : ErrorInfo
    {
        public Error() {
            Tags = new TagSet();
        }

        /// <summary>
        /// Unique id that identifies an error.
        /// </summary>
        public string Id { get; set; }

#if !EMBEDDED
        /// <summary>
        /// The organization that the error belongs to.
        /// </summary>
        public string OrganizationId { get; set; }

        /// <summary>
        /// The project that the error belongs to.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The error stack that the error belongs to.
        /// </summary>
        public string ErrorStackId { get; set; }
#endif

        /// <summary>
        /// The date that the error occurred on.
        /// </summary>
        public DateTimeOffset OccurrenceDate { get; set; }

        /// <summary>
        /// A list of tags used to categorize this error.
        /// </summary>
        public TagSet Tags { get; set; }

        /// <summary>
        /// The email address for the user who experienced the error.
        /// </summary>
        public string UserEmail { get; set; }

        /// <summary>
        /// The user name for the user who experienced the error.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// A description of the error from the user who experienced the error.
        /// </summary>
        public string UserDescription { get; set; }

        /// <summary>
        /// Information about the Exceptionless client to collect the error.
        /// </summary>
        public RequestInfo RequestInfo { get; set; }

        /// <summary>
        /// Information about the Exceptionless client that collected the error.
        /// </summary>
        public ExceptionlessClientInfo ExceptionlessClientInfo { get; set; }

        /// <summary>
        /// Any modules that were loaded / referenced when the error occurred.
        /// </summary>
        public ModuleCollection Modules { get; set; }

        /// <summary>
        /// Information about the machine that the error occurred on.
        /// </summary>
        public EnvironmentInfo EnvironmentInfo { get; set; }

#if !EMBEDDED
        /// <summary>
        /// Wether the error has been marked as fixed or not.
        /// </summary>
        public bool IsFixed { get; set; }

        /// <summary>
        /// Wether the error has been marked as hidden or not.
        /// </summary>
        public bool IsHidden { get; set; }
#endif

        /// <summary>
        /// Marks the error as being a critical occurrence.
        /// </summary>
        public void MarkAsCritical() {
            if (Tags == null)
                Tags = new TagSet();

            Tags.Add("Critical");
        }

        public PersistentEvent ToEvent() {
            var ev = new PersistentEvent {
                Id = Id,
                OrganizationId = OrganizationId,
                ProjectId = ProjectId,
                StackId = ErrorStackId,
                Type = Code == "404" ? Event.KnownTypes.NotFound : Event.KnownTypes.Error,
                Date = OccurrenceDate,
                IsFixed = IsFixed,
                IsHidden = IsHidden,
                Message = Message
            };

            ev.Tags.AddRange(Tags);

            var error = new Exceptionless.Models.Data.Error {
                Message = Message,
                Type = Type,
                Code = Code
            };

            if (StackTrace != null && StackTrace.Count > 0)
                error.StackTrace = StackTrace.ToStackTrace();

            if (TargetMethod != null)
                error.TargetMethod = TargetMethod.ToMethod();

            if (Modules != null && Modules.Count > 0)
                error.Modules = Modules.ToModules();

            if (Inner != null)
                error.Inner = Inner.ToInnerError();

            object traceLog;
            if (ExtendedData != null && ExtendedData.TryGetValue("TraceLog", out traceLog)) {
                ev.Data[Event.KnownDataKeys.TraceLog] = traceLog;
                ExtendedData.Remove("TraceLog");
            }

            if (ExtendedData != null && ExtendedData.Count > 0)
                error.Data.AddRange(ExtendedData.ToData());

            // TODO: __ExceptionInfo 

            ev.Data[Event.KnownDataKeys.Error] = error;

            if (!String.IsNullOrEmpty(UserEmail) || !String.IsNullOrEmpty(UserDescription))
                ev.SetUserDescription(UserEmail, UserDescription);

            if (!String.IsNullOrEmpty(UserName))
                ev.SetUserIdentity(UserName);

            if (RequestInfo != null)
                ev.Data[Event.KnownDataKeys.RequestInfo] = RequestInfo.ToRequestInfo();

            if (EnvironmentInfo != null)
                ev.Data[Event.KnownDataKeys.EnvironmentInfo] = EnvironmentInfo.ToEnvironmentInfo();

            return ev;
        }
    }
}