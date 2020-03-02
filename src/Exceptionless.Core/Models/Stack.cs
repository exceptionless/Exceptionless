using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    [DebuggerDisplay("Id: {Id}, Type: {Type}, Title: {Title}, TotalOccurrences: {TotalOccurrences}")]
    public class Stack : IOwnedByOrganizationAndProjectWithIdentity, IHaveDates {
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
        /// The stack status (ie. open, fixed, regressed, 
        /// </summary>
        public StackStatus Status { get; set; }

        /// <summary>
        /// The date that the stack should be snoozed until.
        /// </summary>
        public DateTime? SnoozeUntilUtc { get; set; }

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

        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public bool AllowNotifications => Status != StackStatus.Ignored && Status != StackStatus.Discarded && Status != StackStatus.Snoozed;

        public static class KnownTypes {
            public const string Error = "error";
            public const string FeatureUsage = "usage";
            public const string SessionHeartbeat = "heartbeat";
            public const string Log = "log";
            public const string NotFound = "404";
            public const string Session = "session";
            public const string SessionEnd = "sessionend";
        }
    }

    public enum StackStatus {
        [EnumMember(Value = "open")] Open,
        [EnumMember(Value = "fixed")] Fixed,
        [EnumMember(Value = "regressed")] Regressed,
        [EnumMember(Value = "snoozed")] Snoozed,
        [EnumMember(Value = "ignored")] Ignored,
        [EnumMember(Value = "discarded")] Discarded
    }
}