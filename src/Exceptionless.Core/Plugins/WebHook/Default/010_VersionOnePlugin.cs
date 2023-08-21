using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

[Priority(10)]
public sealed class VersionOnePlugin : WebHookDataPluginBase
{
    public VersionOnePlugin(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public override Task<object?> CreateFromEventAsync(WebHookDataContext ctx)
    {
        if (!String.Equals(ctx.WebHook.Version, Models.WebHook.KnownVersions.Version1))
            return Task.FromResult<object?>(null);

        var error = ctx.Event?.GetError();
        if (error is null)
            return Task.FromResult<object?>(null);

        var ev = ctx.Event!;
        var requestInfo = ev.GetRequestInfo();
        var environmentInfo = ev.GetEnvironmentInfo();

        return Task.FromResult<object?>(new VersionOneWebHookEvent(_options.BaseURL)
        {
            Id = ev.Id,
            OccurrenceDate = ev.Date,
            Tags = ev.Tags,
            MachineName = environmentInfo?.MachineName,
            RequestPath = requestInfo?.GetFullPath(),
            IpAddress = requestInfo is not null ? requestInfo.ClientIpAddress : environmentInfo?.IpAddress,
            Message = error.Message,
            Type = error.Type,
            Code = error.Code,
            TargetMethod = error.TargetMethod?.GetFullName(),
            ProjectId = ev.ProjectId,
            ProjectName = ctx.Project.Name,
            OrganizationId = ev.OrganizationId,
            OrganizationName = ctx.Organization.Name,
            ErrorStackId = ev.StackId,
            ErrorStackStatus = ctx.Stack.Status,
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

    public override Task<object?> CreateFromStackAsync(WebHookDataContext ctx)
    {
        if (!String.Equals(ctx.WebHook.Version, Models.WebHook.KnownVersions.Version1))
            return Task.FromResult<object?>(null);

        return Task.FromResult<object?>(new VersionOneWebHookStack(_options.BaseURL)
        {
            Id = ctx.Stack.Id,
            Status = ctx.Stack.Status,
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
            IsRegression = ctx.Stack.Status == StackStatus.Regressed,
            IsCritical = ctx.Stack.OccurrencesAreCritical || ctx.Stack.Tags is not null && ctx.Stack.Tags.Contains("Critical"),
            FixedInVersion = ctx.Stack.FixedInVersion
        });
    }

    public record VersionOneWebHookEvent
    {
        private readonly string _baseUrl;

        public VersionOneWebHookEvent(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public string Id { get; init; } = null!;
        public string Url => String.Concat(_baseUrl, "/event/", Id);
        public DateTimeOffset OccurrenceDate { get; init; }
        public TagSet Tags { get; init; } = null!;
        public string? MachineName { get; init; }
        public string? RequestPath { get; init; }
        public string? IpAddress { get; init; }
        public string Message { get; init; } = null!;
        public string Type { get; init; } = null!;
        public string Code { get; init; } = null!;
        public string? TargetMethod { get; init; }
        public string ProjectId { get; init; } = null!;
        public string ProjectName { get; init; } = null!;
        public string OrganizationId { get; init; } = null!;
        public string OrganizationName { get; init; } = null!;
        public string ErrorStackId { get; init; } = null!;
        public StackStatus ErrorStackStatus { get; init; }
        public string ErrorStackUrl => String.Concat(_baseUrl, "/stack/", ErrorStackId);
        public string ErrorStackTitle { get; init; } = null!;
        public string ErrorStackDescription { get; init; } = null!;
        public TagSet ErrorStackTags { get; init; } = null!;
        public int TotalOccurrences { get; init; }
        public DateTime FirstOccurrence { get; init; }
        public DateTime LastOccurrence { get; init; }
        public DateTime? DateFixed { get; init; }
        public bool IsNew { get; init; }
        public bool IsRegression { get; init; }
        public bool IsCritical => Tags is not null && Tags.Contains("Critical");
    }

    public record VersionOneWebHookStack
    {
        private readonly string _baseUrl;

        public VersionOneWebHookStack(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public string Id { get; init; } = null!;
        public StackStatus Status { get; init; }
        public string Url => String.Concat(_baseUrl, "/stack/", Id);
        public string Title { get; init; } = null!;
        public string Description { get; init; } = null!;

        public TagSet Tags { get; init; } = null!;
        public string? RequestPath { get; init; }
        public string? Type { get; init; }
        public string? TargetMethod { get; init; }
        public string ProjectId { get; init; } = null!;
        public string ProjectName { get; init; } = null!;
        public string OrganizationId { get; init; } = null!;
        public string OrganizationName { get; init; } = null!;
        public int TotalOccurrences { get; init; }
        public DateTime FirstOccurrence { get; init; }
        public DateTime LastOccurrence { get; init; }
        public DateTime? DateFixed { get; init; }
        public string? FixedInVersion { get; init; }
        public bool IsRegression { get; init; }
        public bool IsCritical { get; init; }
    }
}
