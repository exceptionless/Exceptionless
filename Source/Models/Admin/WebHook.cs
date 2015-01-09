#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Models.Admin {
    public class WebHook : IOwnedByOrganizationAndProjectWithIdentity {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string Url { get; set; }
        public string[] EventTypes { get; set; }

        /// <summary>
        /// The schema version that should be used.
        /// </summary>
        public Version Version { get; set; }
    }
}