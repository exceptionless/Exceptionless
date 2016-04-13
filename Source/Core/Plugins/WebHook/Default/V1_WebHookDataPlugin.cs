using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;

namespace Exceptionless.Core.Plugins.WebHook {
    [Priority(10)]
    public sealed class VersionOne : WebHookDataPluginBase {
        public override Task<object> CreateFromEventAsync(WebHookDataContext ctx) {
            if (ctx.Version.Major != 1)
                return Task.FromResult<object>(null);

            var error = ctx.Event.GetError();
            if (error == null)
                return Task.FromResult<object>(null);

            var requestInfo = ctx.Event.GetRequestInfo();
            var environmentInfo = ctx.Event.GetEnvironmentInfo();

            return Task.FromResult<object>(new VersionOneWebHookEvent {
                Id = ctx.Event.Id,
                OccurrenceDate = ctx.Event.Date,
                Tags = ctx.Event.Tags,
                MachineName = environmentInfo?.MachineName,
                RequestPath = requestInfo?.GetFullPath(),
                IpAddress = requestInfo != null ? requestInfo.ClientIpAddress : environmentInfo?.IpAddress,
                Message = error.Message,
                Type = error.Type,
                Code = error.Code,
                TargetMethod = error.TargetMethod?.GetFullName(),
                ProjectId = ctx.Event.ProjectId,
                ProjectName = ctx.Project.Name,
                OrganizationId = ctx.Event.OrganizationId,
                OrganizationName = ctx.Organization.Name,
                ErrorStackId = ctx.Event.StackId,
                ErrorStackTitle = ctx.Stack.Title,
                ErrorStackDescription = ctx.Stack.Description,
                ErrorStackTags = ctx.Stack.Tags,
                TotalOccurrences = ctx.Stack.TotalOccurrences,
                FirstOccurrence = ctx.Stack.FirstOccurrence,
                LastOccurrence = ctx.Stack.LastOccurrence,
                DateFixed = ctx.Stack.DateFixed,
                IsRegression = ctx.IsRegression,
                IsNew = ctx.IsNew
            });
        }

        public override Task<object> CreateFromStackAsync(WebHookDataContext ctx) {
            if (ctx.Version.Major != 1)
                return Task.FromResult<object>(null);

              return Task.FromResult<object>(new VersionOneWebHookStack {
                Id = ctx.Stack.Id,
                Title = ctx.Stack.Title,
                Description = ctx.Stack.Description,
                Tags = ctx.Stack.Tags,
                RequestPath = ctx.Stack.SignatureInfo.ContainsKey("Path") ? ctx.Stack.SignatureInfo["Path"] : null,
                Type = ctx.Stack.SignatureInfo.ContainsKey("ExceptionType") ? ctx.Stack.SignatureInfo["ExceptionType"] : null,
                TargetMethod = ctx.Stack.SignatureInfo.ContainsKey("Method") ? ctx.Stack.SignatureInfo["Method"] : null,
                ProjectId = ctx.Stack.ProjectId,
                ProjectName = ctx.Project.Name,
                OrganizationId = ctx.Stack.OrganizationId,
                OrganizationName = ctx.Organization.Name,
                TotalOccurrences = ctx.Stack.TotalOccurrences,
                FirstOccurrence = ctx.Stack.FirstOccurrence,
                LastOccurrence = ctx.Stack.LastOccurrence,
                DateFixed = ctx.Stack.DateFixed,
                IsRegression = ctx.Stack.IsRegressed,
                IsCritical = ctx.Stack.OccurrencesAreCritical || ctx.Stack.Tags != null && ctx.Stack.Tags.Contains("Critical"),
                FixedInVersion = ctx.Stack.FixedInVersion
            });
        }

        public class VersionOneWebHookEvent {
            public string Id { get; set; }
            public string Url => String.Concat(Settings.Current.BaseURL, "/event/", Id);
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
            public string ErrorStackUrl => String.Concat(Settings.Current.BaseURL, "/stack/", ErrorStackId);
            public string ErrorStackTitle { get; set; }
            public string ErrorStackDescription { get; set; }
            public TagSet ErrorStackTags { get; set; }
            public int TotalOccurrences { get; set; }
            public DateTime FirstOccurrence { get; set; }
            public DateTime LastOccurrence { get; set; }
            public DateTime? DateFixed { get; set; }
            public bool IsNew { get; set; }
            public bool IsRegression { get; set; }
            public bool IsCritical => Tags != null && Tags.Contains("Critical");
        }

        public class VersionOneWebHookStack {
            public string Id { get; set; }
            public string Url => String.Concat(Settings.Current.BaseURL, "/stack/", Id);
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
}