#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Models {
    public class WebHookStack {
        public string Id { get; set; }
        public string Url { get { return String.Concat(Settings.Current.BaseURL, "/stack/", Id); } }
        public string Title { get; set; }
        public string Description { get; set; }

        public TagSet Tags { get; set; }
        public string RequestPath { get; set; }
        public string Type { get; set; }
        public string TargetMethod { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public int TotalOccurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public DateTime? DateFixed { get; set; }
        public string FixedInVersion { get; set; }
        public bool IsRegression { get; set; }
        public bool IsCritical { get; set; }
    }
}