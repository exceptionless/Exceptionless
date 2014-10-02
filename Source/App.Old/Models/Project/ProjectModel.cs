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
using System.ComponentModel.DataAnnotations;
using Exceptionless.Models;
using Foolproof;

namespace Exceptionless.App.Models.Project {
    public class ProjectModel {
        public string Id { get; set; }

        public string UserId { get; set; }

        [Display(Name = "Project Name")]
        [Required(ErrorMessage = "Project Name is required.")]
        //[Remote("IsNameAvailable", "Project", AdditionalFields = "Id", ErrorMessage = "A Project with this name already exists.")]
        public string Name { get; set; }

        [Display(Name = "Time Zone")]
        [Required(ErrorMessage = "Time Zone is required.")]
        public string TimeZone { get; set; }

        [Display(Name = "Organization Name")]
        public string OrganizationId { get; set; }

        [Display(Name = "Organization Name")]
        [RequiredIf("OrganizationId", "", ErrorMessage = "Organization Name is required.")]
        public string OrganizationName { get; set; }

        [Display(Name = "API Keys")]
        public HashSet<string> ApiKeys { get; set; }

        [Display(Name = "Custom Page Content")]
        public string CustomContent { get; set; }

        [Display(Name = "Configuration Settings")]
        public ClientConfiguration Configuration { get; set; }

        public NotificationMode Mode { get; set; }
        public bool SendDailySummary { get; set; }
        public bool ReportCriticalErrors { get; set; }
        public bool ReportRegressions { get; set; }
        public bool Report404Errors { get; set; }
        public bool ReportKnownBotErrors { get; set; }
    }
}