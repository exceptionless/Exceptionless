#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.EventMigration.Models {
    public class ExceptionlessClientInfo {
        /// <summary>
        /// The version of the Exceptionless client that processed this error.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A unique identifier for this installation of the Exceptionless client.
        /// </summary>
        public string InstallIdentifier { get; set; }

        /// <summary>
        /// The date that this installation of the Exceptionless client was first used.
        /// </summary>
        public DateTimeOffset InstallDate { get; set; }

        /// <summary>
        /// The number of times the Exceptionless client has been started since it was first installed.
        /// </summary>
        public int StartCount { get; set; }

        /// <summary>
        /// The number of error submissions the Exceptionless client has completed since it was first installed.
        /// </summary>
        public int SubmitCount { get; set; }

        /// <summary>
        /// The Exceptionless client platform.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// What submission method was used to collect the error information.
        /// </summary>
        public string SubmissionMethod { get; set; }
    }
}