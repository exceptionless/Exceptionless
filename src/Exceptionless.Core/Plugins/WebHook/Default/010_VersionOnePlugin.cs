using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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

    public static JsonSerializerOptions CreateJsonSerializerOptions(JsonSerializerOptions jsonOptions)
    {
        return new JsonSerializerOptions(jsonOptions)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
    }

    public override Task<object?> CreateFromEventAsync(WebHookDataContext ctx)
    {
        if (!String.Equals(ctx.WebHook.Version, Models.WebHook.KnownVersions.Version1))
            return Task.FromResult<object?>(null);

        var error = ctx.Event?.GetError(_serializer, _logger);
        if (error is null)
            return Task.FromResult<object?>(null);

        var ev = ctx.Event!;
        var requestInfo = ev.GetRequestInfo(_serializer, _logger);
        var environmentInfo = ev.GetEnvironmentInfo(_serializer, _logger);

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
            RequestPath = ctx.Stack.SignatureInfo.TryGetValue("Path", out string? path) ? path : null,
            Type = ctx.Stack.SignatureInfo.TryGetValue("ExceptionType", out string? exceptionType) ? exceptionType : null,
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
            IsCritical = ctx.Stack.OccurrencesAreCritical || ctx.Stack.Tags is not null && ctx.Stack.Tags.Contains(Event.KnownTags.Critical),
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

        [JsonPropertyName(nameof(Id))]
        public string Id { get; init; } = null!;
        [JsonPropertyName(nameof(Url))]
        public string Url => String.Concat(_baseUrl, "/event/", Id);
        [JsonPropertyName(nameof(OccurrenceDate))]
        public DateTimeOffset OccurrenceDate { get; init; }
        [JsonPropertyName(nameof(Tags))]
        public TagSet? Tags { get; init; } = null!;
        [JsonPropertyName(nameof(MachineName))]
        public string? MachineName { get; init; }
        [JsonPropertyName(nameof(RequestPath))]
        public string? RequestPath { get; init; }
        [JsonPropertyName(nameof(IpAddress))]
        public string? IpAddress { get; init; }
        [JsonPropertyName(nameof(Message))]
        public string? Message { get; init; } = null!;
        [JsonPropertyName(nameof(Type))]
        public string? Type { get; init; } = null!;
        [JsonPropertyName(nameof(Code))]
        public string? Code { get; init; } = null!;
        [JsonPropertyName(nameof(TargetMethod))]
        public string? TargetMethod { get; init; }
        [JsonPropertyName(nameof(ProjectId))]
        public string ProjectId { get; init; } = null!;
        [JsonPropertyName(nameof(ProjectName))]
        public string ProjectName { get; init; } = null!;
        [JsonPropertyName(nameof(OrganizationId))]
        public string OrganizationId { get; init; } = null!;
        [JsonPropertyName(nameof(OrganizationName))]
        public string OrganizationName { get; init; } = null!;
        [JsonPropertyName(nameof(ErrorStackId))]
        public string ErrorStackId { get; init; } = null!;
        [JsonPropertyName(nameof(ErrorStackStatus))]
        public StackStatus ErrorStackStatus { get; init; }
        [JsonPropertyName(nameof(ErrorStackUrl))]
        public string ErrorStackUrl => String.Concat(_baseUrl, "/stack/", ErrorStackId);
        [JsonPropertyName(nameof(ErrorStackTitle))]
        public string ErrorStackTitle { get; init; } = null!;
        [JsonPropertyName(nameof(ErrorStackDescription))]
        public string? ErrorStackDescription { get; init; } = null!;
        [JsonPropertyName(nameof(ErrorStackTags))]
        public TagSet ErrorStackTags { get; init; } = null!;
        [JsonPropertyName(nameof(TotalOccurrences))]
        public int TotalOccurrences { get; init; }
        [JsonPropertyName(nameof(FirstOccurrence))]
        public DateTime FirstOccurrence { get; init; }
        [JsonPropertyName(nameof(LastOccurrence))]
        public DateTime LastOccurrence { get; init; }
        [JsonPropertyName(nameof(DateFixed))]
        public DateTime? DateFixed { get; init; }
        [JsonPropertyName(nameof(IsNew))]
        public bool IsNew { get; init; }
        [JsonPropertyName(nameof(IsRegression))]
        public bool IsRegression { get; init; }
        [JsonPropertyName(nameof(IsCritical))]
        public bool IsCritical => Tags is not null && Tags.Contains(Event.KnownTags.Critical);
    }

    public record VersionOneWebHookStack
    {
        private readonly string _baseUrl;

        public VersionOneWebHookStack(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        [JsonPropertyName(nameof(Id))]
        public string Id { get; init; } = null!;
        [JsonPropertyName(nameof(Status))]
        public StackStatus Status { get; init; }
        [JsonPropertyName(nameof(Url))]
        public string Url => String.Concat(_baseUrl, "/stack/", Id);
        [JsonPropertyName(nameof(Title))]
        public string Title { get; init; } = null!;
        [JsonPropertyName(nameof(Description))]
        public string? Description { get; init; } = null!;
        [JsonPropertyName(nameof(Tags))]
        public TagSet Tags { get; init; } = null!;
        [JsonPropertyName(nameof(RequestPath))]
        public string? RequestPath { get; init; }
        [JsonPropertyName(nameof(Type))]
        public string? Type { get; init; }
        [JsonPropertyName(nameof(TargetMethod))]
        public string? TargetMethod { get; init; }
        [JsonPropertyName(nameof(ProjectId))]
        public string ProjectId { get; init; } = null!;
        [JsonPropertyName(nameof(ProjectName))]
        public string ProjectName { get; init; } = null!;
        [JsonPropertyName(nameof(OrganizationId))]
        public string OrganizationId { get; init; } = null!;
        [JsonPropertyName(nameof(OrganizationName))]
        public string OrganizationName { get; init; } = null!;
        [JsonPropertyName(nameof(TotalOccurrences))]
        public int TotalOccurrences { get; init; }
        [JsonPropertyName(nameof(FirstOccurrence))]
        public DateTime FirstOccurrence { get; init; }
        [JsonPropertyName(nameof(LastOccurrence))]
        public DateTime LastOccurrence { get; init; }
        [JsonPropertyName(nameof(DateFixed))]
        public DateTime? DateFixed { get; init; }
        [JsonPropertyName(nameof(FixedInVersion))]
        public string? FixedInVersion { get; init; }
        [JsonPropertyName(nameof(IsRegression))]
        public bool IsRegression { get; init; }
        [JsonPropertyName(nameof(IsCritical))]
        public bool IsCritical { get; init; }
    }
}
