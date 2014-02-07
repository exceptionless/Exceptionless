#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Web.Models.Project {
    public class ProjectInfoModel {
        public string Id { get; set; }
        public string Name { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public double TimeZoneOffset { get; set; }

        public long StackCount { get; set; }
        public long ErrorCount { get; set; }
        public long TotalErrorCount { get; set; }
    }
}