using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

[Priority(20)]
public sealed class VersionTwoPlugin : WebHookDataPluginBase
{
    public VersionTwoPlugin(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public override Task<object?> CreateFromEventAsync(WebHookDataContext ctx)
    {
        if (!String.Equals(ctx.WebHook.Version, Models.WebHook.KnownVersions.Version2))
            return Task.FromResult<object?>(null);

        var ev = ctx.Event!;
        return Task.FromResult<object?>(new WebHookEvent(_options.BaseURL)
        {
            Id = ev.Id,
            OccurrenceDate = ev.Date,
            Tags = ev.Tags,
            Message = ev.Message,
            Type = ev.Type,
            Source = ev.Source,
            ProjectId = ev.ProjectId,
            ProjectName = ctx.Project.Name,
            OrganizationId = ev.OrganizationId,
            OrganizationName = ctx.Organization.Name,
            StackId = ev.StackId,
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

    public override Task<object?> CreateFromStackAsync(WebHookDataContext ctx)
    {
        if (!String.Equals(ctx.WebHook.Version, Models.WebHook.KnownVersions.Version2))
            return Task.FromResult<object?>(null);

        return Task.FromResult<object?>(new WebHookStack(_options.BaseURL)
        {
            Id = ctx.Stack.Id,
            Title = ctx.Stack.Title,
            Description = ctx.Stack.Description,
            Tags = ctx.Stack.Tags,
            RequestPath = ctx.Stack.SignatureInfo.TryGetValue("Path", out string? path) ? path : null,
            Type = ctx.Stack.SignatureInfo.TryGetValue("ExceptionType", out string? type) ? type : null,
            TargetMethod = ctx.Stack.SignatureInfo.TryGetValue("Method", out string? method) ? method : null,
            ProjectId = ctx.Stack.ProjectId,
            ProjectName = ctx.Project.Name,
            OrganizationId = ctx.Stack.OrganizationId,
            OrganizationName = ctx.Organization.Name,
            TotalOccurrences = ctx.Stack.TotalOccurrences,
            FirstOccurrence = ctx.Stack.FirstOccurrence,
            LastOccurrence = ctx.Stack.LastOccurrence,
            DateFixed = ctx.Stack.DateFixed,
            IsRegression = ctx.Stack.Status == StackStatus.Regressed,
            IsCritical = ctx.Stack.OccurrencesAreCritical || ctx.Stack.Tags is not null && ctx.Stack.Tags.Contains("Critical"),
            FixedInVersion = ctx.Stack.FixedInVersion
        });
    }
}
