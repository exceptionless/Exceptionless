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

namespace Exceptionless.EventMigration.Models {
    public class ErrorStack {
        public ErrorStack() {
            Tags = new TagSet();
            References = new Collection<string>();
        }

        /// <summary>
        /// Unique id that identifies an error stack.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The organization that the error stack belongs to.
        /// </summary>
        public string OrganizationId { get; set; }

        /// <summary>
        /// The project that the error stack belongs to.
        /// </summary>
        public string ProjectId { get; set; }

        public string Type { get; set; }

        /// <summary>
        /// The error signature used for stacking future occurrences.
        /// </summary>
        public string SignatureHash { get; set; }

        /// <summary>
        /// The collection of information that went into creating the signature hash for the stack.
        /// </summary>
        public ConfigurationDictionary SignatureInfo { get; set; }

        /// <summary>
        /// The version the error was fixed in.
        /// </summary>
        public string FixedInVersion { get; set; }

        /// <summary>
        /// The date the error was fixed.
        /// </summary>
        public DateTime? DateFixed { get; set; }

        /// <summary>
        /// The error title.
        /// </summary>
        public string Title { get; set; }

        public int TotalOccurrences { get; set; }

        /// <summary>
        /// The date of the 1st occurrence of this error in UTC time.
        /// </summary>
        public DateTime FirstOccurrence { get; set; }

        /// <summary>
        /// The date of the last occurrence of this error in UTC time.
        /// </summary>
        public DateTime LastOccurrence { get; set; }

        /// <summary>
        /// The error description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// If true, notifications will not be sent for this stack.
        /// </summary>
        public bool DisableNotifications { get; set; }

        /// <summary>
        /// Controls whether error occurrences are hidden from reports.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// If true, the stack was previously marked as fixed and a new error came in.
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
        /// A list of tags used to categorize this error.
        /// </summary>
        public TagSet Tags { get; set; }
    }
}