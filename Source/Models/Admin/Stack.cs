#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Exceptionless.Models {
    public class Stack : IOwnedByOrganization, IOwnedByProject, IIdentity {
        public Stack() {
            Tags = new TagSet();
            References = new Collection<string>();
            SignatureInfo = new SettingsDictionary();
        }

        /// <summary>
        /// Unique id that identifies a stack.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The organization that the stack belongs to.
        /// </summary>
        public string OrganizationId { get; set; }

        /// <summary>
        /// The project that the stack belongs to.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The stack type (ie. error, log message, feature usage). Check <see cref="KnownTypes">Stack.KnownTypes</see> for standard stack types.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The signature used for stacking future occurrences.
        /// </summary>
        public string SignatureHash { get; set; }

        /// <summary>
        /// The collection of information that went into creating the signature hash for the stack.
        /// </summary>
        public SettingsDictionary SignatureInfo { get; set; }

        /// <summary>
        /// The version the stack was fixed in.
        /// </summary>
        public string FixedInVersion { get; set; }

        /// <summary>
        /// The date the stack was fixed.
        /// </summary>
        public DateTime? DateFixed { get; set; }

        /// <summary>
        /// The stack title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The total number of occurrences in the stack.
        /// </summary>
        public int TotalOccurrences { get; set; }

        /// <summary>
        /// The date of the 1st occurrence of this stack in UTC time.
        /// </summary>
        public DateTime FirstOccurrence { get; set; }

        /// <summary>
        /// The date of the last occurrence of this stack in UTC time.
        /// </summary>
        public DateTime LastOccurrence { get; set; }

        /// <summary>
        /// The stack description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// If true, notifications will not be sent for this stack.
        /// </summary>
        public bool DisableNotifications { get; set; }

        /// <summary>
        /// Controls whether occurrences are hidden from reports.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// If true, the stack was previously marked as fixed and a new occurrence came in.
        /// </summary>
        public bool IsRegressed { get; set; }

        /// <summary>
        /// If true, all future occurrences will be marked as critical.
        /// </summary>
        public bool OccurrencesAreCritical { get; set; }

        /// <summary>
        /// A list of references.
        /// </summary>
        public ICollection<string> References { get; set; }

        /// <summary>
        /// A list of tags used to categorize this stack.
        /// </summary>
        public TagSet Tags { get; set; }

        public static class KnownTypes {
            public const string Error = "error";
            public const string NotFound = "404";
            public const string Log = "log";
            public const string FeatureUsage = "usage";
            public const string SessionStart = "start";
            public const string SessionEnd = "end";
        }
    }
}