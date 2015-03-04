using System;
using System.Diagnostics;
using ModuleCollection = Exceptionless.EventMigration.Models.Collections.ModuleCollection;

namespace Exceptionless.EventMigration.Models {
    [DebuggerDisplay("{Id}, {OccurrenceDate}")]
    public class Error : ErrorInfo
    {
        public Error() {
            Tags = new TagSet();
        }

        /// <summary>
        /// Unique id that identifies an error.
        /// </summary>
        public string Id { get; set; }

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

        /// <summary>
        /// Wether the error has been marked as fixed or not.
        /// </summary>
        public bool IsFixed { get; set; }

        /// <summary>
        /// Wether the error has been marked as hidden or not.
        /// </summary>
        public bool IsHidden { get; set; }

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