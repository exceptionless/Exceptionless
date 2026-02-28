using System.Text.Json.Serialization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

[Priority(10)]
public sealed class VersionOnePlugin : WebHookDataPluginBase
{
    private readonly ITextSerializer _serializer;

    public VersionOnePlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public override Task<object?> CreateFromEventAsync(WebHookDataContext ctx)
    {
        if (!String.Equals(ctx.WebHook.Version, Models.WebHook.KnownVersions.Version1))
            return Task.FromResult<object?>(null);

        var error = ctx.Event?.GetError(_serializer);
        if (error is null)
            return Task.FromResult<object?>(null);

        var ev = ctx.Event!;
        var requestInfo = ev.GetRequestInfo(_serializer);
        var environmentInfo = ev.GetEnvironmentInfo(_serializer);

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

        [JsonPropertyName("Id")]
        public string Id { get; init; } = null!;
        [JsonPropertyName("Url")]
        public string Url => String.Concat(_baseUrl, "/event/", Id);
        [JsonPropertyName("OccurrenceDate")]
        public DateTimeOffset OccurrenceDate { get; init; }
        [JsonPropertyName("Tags")]
        public TagSet? Tags { get; init; } = null!;
        [JsonPropertyName("MachineName")]
        public string? MachineName { get; init; }
        [JsonPropertyName("RequestPath")]
        public string? RequestPath { get; init; }
        [JsonPropertyName("IpAddress")]
        public string? IpAddress { get; init; }
        [JsonPropertyName("Message")]
        public string? Message { get; init; } = null!;
        [JsonPropertyName("Type")]
        public string? Type { get; init; } = null!;
        [JsonPropertyName("Code")]
        public string? Code { get; init; } = null!;
        [JsonPropertyName("TargetMethod")]
        public string? TargetMethod { get; init; }
        [JsonPropertyName("ProjectId")]
        public string ProjectId { get; init; } = null!;
        [JsonPropertyName("ProjectName")]
        public string ProjectName { get; init; } = null!;
        [JsonPropertyName("OrganizationId")]
        public string OrganizationId { get; init; } = null!;
        [JsonPropertyName("OrganizationName")]
        public string OrganizationName { get; init; } = null!;
        [JsonPropertyName("ErrorStackId")]
        public string ErrorStackId { get; init; } = null!;
        [JsonPropertyName("ErrorStackStatus")]
        public StackStatus ErrorStackStatus { get; init; }
        [JsonPropertyName("ErrorStackUrl")]
        public string ErrorStackUrl => String.Concat(_baseUrl, "/stack/", ErrorStackId);
        [JsonPropertyName("ErrorStackTitle")]
        public string ErrorStackTitle { get; init; } = null!;
        [JsonPropertyName("ErrorStackDescription")]
        public string? ErrorStackDescription { get; init; } = null!;
        [JsonPropertyName("ErrorStackTags")]
        public TagSet ErrorStackTags { get; init; } = null!;
        [JsonPropertyName("TotalOccurrences")]
        public int TotalOccurrences { get; init; }
        [JsonPropertyName("FirstOccurrence")]
        public DateTime FirstOccurrence { get; init; }
        [JsonPropertyName("LastOccurrence")]
        public DateTime LastOccurrence { get; init; }
        [JsonPropertyName("DateFixed")]
        public DateTime? DateFixed { get; init; }
        [JsonPropertyName("IsNew")]
        public bool IsNew { get; init; }
        [JsonPropertyName("IsRegression")]
        public bool IsRegression { get; init; }
        [JsonPropertyName("IsCritical")]
        public bool IsCritical => Tags is not null && Tags.Contains("Critical");
    }

    public record VersionOneWebHookStack
    {
        private readonly string _baseUrl;

        public VersionOneWebHookStack(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        [JsonPropertyName("Id")]
        public string Id { get; init; } = null!;
        [JsonPropertyName("Status")]
        public StackStatus Status { get; init; }
        [JsonPropertyName("Url")]
        public string Url => String.Concat(_baseUrl, "/stack/", Id);
        [JsonPropertyName("Title")]
        public string Title { get; init; } = null!;
        [JsonPropertyName("Description")]
        public string? Description { get; init; } = null!;
        [JsonPropertyName("Tags")]
        public TagSet Tags { get; init; } = null!;
        [JsonPropertyName("RequestPath")]
        public string? RequestPath { get; init; }
        [JsonPropertyName("Type")]
        public string? Type { get; init; }
        [JsonPropertyName("TargetMethod")]
        public string? TargetMethod { get; init; }
        [JsonPropertyName("ProjectId")]
        public string ProjectId { get; init; } = null!;
        [JsonPropertyName("ProjectName")]
        public string ProjectName { get; init; } = null!;
        [JsonPropertyName("OrganizationId")]
        public string OrganizationId { get; init; } = null!;
        [JsonPropertyName("OrganizationName")]
        public string OrganizationName { get; init; } = null!;
        [JsonPropertyName("TotalOccurrences")]
        public int TotalOccurrences { get; init; }
        [JsonPropertyName("FirstOccurrence")]
        public DateTime FirstOccurrence { get; init; }
        [JsonPropertyName("LastOccurrence")]
        public DateTime LastOccurrence { get; init; }
        [JsonPropertyName("DateFixed")]
        public DateTime? DateFixed { get; init; }
        [JsonPropertyName("FixedInVersion")]
        public string? FixedInVersion { get; init; }
        [JsonPropertyName("IsRegression")]
        public bool IsRegression { get; init; }
        [JsonPropertyName("IsCritical")]
        public bool IsCritical { get; init; }
    }
}
