using System;
using System.Threading.Tasks;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.WebHook {
    [Priority(20)]
    public sealed class VersionTwo : WebHookDataPluginBase {
        public override Task<object> CreateFromEventAsync(WebHookDataContext ctx) {
            if (ctx.Version.Major != 2)
                return Task.FromResult<object>(null);

            return Task.FromResult<object>(new WebHookEvent {
                Id = ctx.Event.Id,
                OccurrenceDate = ctx.Event.Date,
                Tags = ctx.Event.Tags,
                Message = ctx.Event.Message,
                Type = ctx.Event.Type,
                Source = ctx.Event.Source,
                ProjectId = ctx.Event.ProjectId,
                ProjectName = ctx.Project.Name,
                OrganizationId = ctx.Event.OrganizationId,
                OrganizationName = ctx.Organization.Name,
                StackId = ctx.Event.StackId,
                StackTitle = ctx.Stack.Title,
                StackDescription = ctx.Stack.Description,
                StackTags = ctx.Stack.Tags,
                TotalOccurrences = ctx.Stack.TotalOccurrences,
                FirstOccurrence = ctx.Stack.FirstOccurrence,
                LastOccurrence = ctx.Stack.LastOccurrence,
                DateFixed = ctx.Stack.DateFixed,
                IsRegression = ctx.IsRegression,
                IsNew = ctx.IsNew
            });
        }

        public override Task<object> CreateFromStackAsync(WebHookDataContext ctx) {
            if (ctx.Version.Major != 2)
                return Task.FromResult<object>(null);

            return Task.FromResult<object>(new WebHookStack {
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
    }
}