#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Models {
    public class ExceptionlessClientInfo {
        /// <summary>
        /// The version of the Exceptionless client that processed this event.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The Exceptionless client platform.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// What submission method was used to collect the event information.
        /// </summary>
        public string Method { get; set; }
    }
}