#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models.Collections;

namespace Exceptionless.Models {
    public class Error : ErrorInfo
#if !EMBEDDED
        , IOwnedByOrganization
#endif
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

        /// <summary>
        /// The date this error occurrence was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; }
#endif

        /// <summary>
        /// Marks the error as being a critical occurrence.
        /// </summary>
        public void MarkAsCritical() {
            if (Tags == null)
                Tags = new TagSet();

            Tags.Add("Critical");
        }
    }
}