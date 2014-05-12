#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Plugins.EventPipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Core.Models {
    public class WebHookEvent {
        public string Id { get; set; }
        public string Url { get { return String.Concat(Settings.Current.BaseURL, "/event/", StackId, "/", Id); } }
        public DateTimeOffset OccurrenceDate { get; set; }
        public TagSet Tags { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string StackId { get; set; }
        public string StackUrl { get { return String.Concat(Settings.Current.BaseURL, "/stack/", StackId); } }
        public string StackTitle { get; set; }
        public string StackDescription { get; set; }
        public TagSet StackTags { get; set; }
        public int TotalOccurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public DateTime? DateFixed { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
        public bool IsCritical { get { return Tags != null && Tags.Contains("Critical"); } }

        public static WebHookEvent FromEvent(EventContext ctx, IProjectRepository projectRepository, IStackRepository stackRepository, IOrganizationRepository organizationRepository) {
            if (ctx == null || ctx.Event == null)
                throw new ArgumentNullException("ctx");

            if (projectRepository == null)
                throw new ArgumentNullException("projectRepository");

            if (stackRepository == null)
                throw new ArgumentNullException("stackRepository");

            if (organizationRepository == null)
                throw new ArgumentNullException("organizationRepository");

            var project = projectRepository.GetById(ctx.Event.ProjectId);
            if (project == null)
                throw new ArgumentException("ProjectId not found.");

            var organization = organizationRepository.GetById(ctx.Event.OrganizationId);
            if (organization == null)
                throw new ArgumentException("OrganizationId not found.");

            var errorStack = stackRepository.GetById(ctx.Event.StackId);
            if (errorStack == null)
                throw new ArgumentException("ErrorStackId not found.");

            return new WebHookEvent {
                Id = ctx.Event.Id,
                OccurrenceDate = ctx.Event.Date,
                Tags = ctx.Event.Tags,
                Message = ctx.Event.Message,
                Type = ctx.Event.Type,
                Source = ctx.Event.Source,
                ProjectId = ctx.Event.ProjectId,
                ProjectName = project.Name,
                OrganizationId = ctx.Event.OrganizationId,
                OrganizationName = organization.Name,
                StackId = ctx.Event.StackId,
                StackTitle = errorStack.Title,
                StackDescription = errorStack.Description,
                StackTags = errorStack.Tags,
                TotalOccurrences = errorStack.TotalOccurrences,
                FirstOccurrence = errorStack.FirstOccurrence,
                LastOccurrence = errorStack.LastOccurrence,
                DateFixed = errorStack.DateFixed,
                IsRegression = ctx.IsRegression,
                IsNew = ctx.IsNew
            };
        }
    }
}