#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;

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

        public static WebHookStack FromStack(Stack stack, IProjectRepository projectRepository, IOrganizationRepository organizationRepository) {
            if (stack == null)
                throw new ArgumentNullException("stack");

            if (projectRepository == null)
                throw new ArgumentNullException("projectRepository");

            if (organizationRepository == null)
                throw new ArgumentNullException("organizationRepository");

            var project = projectRepository.GetByIdCached(stack.ProjectId);
            if (project == null)
                throw new ArgumentException("ProjectId not found.");

            var organization = organizationRepository.GetByIdCached(stack.OrganizationId);
            if (organization == null)
                throw new ArgumentException("OrganizationId not found.");

            return new WebHookStack {
                Id = stack.Id,
                Title = stack.Title,
                Description = stack.Description,
                Tags = stack.Tags,
                RequestPath = stack.SignatureInfo.ContainsKey("Path") ? stack.SignatureInfo["Path"] : null,
                Type = stack.SignatureInfo.ContainsKey("ExceptionType") ? stack.SignatureInfo["ExceptionType"] : null,
                TargetMethod = stack.SignatureInfo.ContainsKey("Method") ? stack.SignatureInfo["Method"] : null,
                ProjectId = stack.ProjectId,
                ProjectName = project.Name,
                OrganizationId = stack.OrganizationId,
                OrganizationName = organization.Name,
                TotalOccurrences = stack.TotalOccurrences,
                FirstOccurrence = stack.FirstOccurrence,
                LastOccurrence = stack.LastOccurrence,
                DateFixed = stack.DateFixed,
                IsRegression = stack.IsRegressed,
                IsCritical = stack.OccurrencesAreCritical || stack.Tags != null && stack.Tags.Contains("Critical"),
                FixedInVersion = stack.FixedInVersion
            };
        }
    }
}