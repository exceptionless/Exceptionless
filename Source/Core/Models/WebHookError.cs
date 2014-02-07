#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Pipeline;
using Exceptionless.Models;

namespace Exceptionless.Core.Models {
    public class WebHookError {
        public string Id { get; set; }
        public string Url { get { return String.Concat(Settings.Current.BaseURL, "/error/", ErrorStackId, "/", Id); } }
        public DateTimeOffset OccurrenceDate { get; set; }
        public TagSet Tags { get; set; }
        public string MachineName { get; set; }
        public string RequestPath { get; set; }
        public string IpAddress { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public string Code { get; set; }
        public string TargetMethod { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string ErrorStackId { get; set; }
        public string ErrorStackUrl { get { return String.Concat(Settings.Current.BaseURL, "/stack/", ErrorStackId); } }
        public string ErrorStackTitle { get; set; }
        public string ErrorStackDescription { get; set; }
        public TagSet ErrorStackTags { get; set; }
        public int TotalOccurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public DateTime? DateFixed { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
        public bool IsCritical { get { return Tags != null && Tags.Contains("Critical"); } }

        public static WebHookError FromError(ErrorPipelineContext ctx, IProjectRepository projectRepository, IErrorStackRepository errorStackRepository, IOrganizationRepository organizationRepository) {
            if (ctx == null || ctx.Error == null)
                throw new ArgumentNullException("ctx");

            if (projectRepository == null)
                throw new ArgumentNullException("projectRepository");

            if (errorStackRepository == null)
                throw new ArgumentNullException("errorStackRepository");

            if (organizationRepository == null)
                throw new ArgumentNullException("organizationRepository");

            var project = projectRepository.GetByIdCached(ctx.Error.ProjectId);
            if (project == null)
                throw new ArgumentException("ProjectId not found.");

            var organization = organizationRepository.GetByIdCached(ctx.Error.OrganizationId);
            if (organization == null)
                throw new ArgumentException("OrganizationId not found.");

            var errorStack = errorStackRepository.GetByIdCached(ctx.Error.ErrorStackId);
            if (errorStack == null)
                throw new ArgumentException("ErrorStackId not found.");

            return new WebHookError {
                Id = ctx.Error.Id,
                OccurrenceDate = ctx.Error.OccurrenceDate,
                Tags = ctx.Error.Tags,
                MachineName = ctx.Error.EnvironmentInfo != null ? ctx.Error.EnvironmentInfo.MachineName : null,
                RequestPath = ctx.Error.RequestInfo != null ? ctx.Error.RequestInfo.GetFullPath() : null,
                IpAddress = ctx.Error.RequestInfo != null ? ctx.Error.RequestInfo.ClientIpAddress : ctx.Error.EnvironmentInfo != null ? ctx.Error.EnvironmentInfo.IpAddress : null,
                Message = ctx.Error.Message,
                Type = ctx.Error.Type,
                Code = ctx.Error.Code,
                TargetMethod = ctx.Error.TargetMethod != null ? ctx.Error.TargetMethod.FullName : null,
                ProjectId = ctx.Error.ProjectId,
                ProjectName = project.Name,
                OrganizationId = ctx.Error.OrganizationId,
                OrganizationName = organization.Name,
                ErrorStackId = ctx.Error.ErrorStackId,
                ErrorStackTitle = errorStack.Title,
                ErrorStackDescription = errorStack.Description,
                ErrorStackTags = errorStack.Tags,
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