using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Mcp;

public sealed record McpResponse<T>(bool Ok, T? Data = default, McpErrorInfo? Error = null, string? Warning = null, McpPagination? Pagination = null)
{
    public static McpResponse<T> Success(T data, string? warning = null, McpPagination? pagination = null)
    {
        return new McpResponse<T>(true, data, Warning: warning, Pagination: pagination);
    }

    public static McpResponse<T> Failed(McpErrorInfo error)
    {
        return new McpResponse<T>(false, Error: error);
    }
}

public sealed record McpListData<T>(IReadOnlyCollection<T> Items);

public sealed record McpPagination(bool HasMore, int Limit, string? Before = null, string? After = null);

public sealed record McpContextResult(
    string? ActiveOrganizationId,
    string? ActiveOrganizationName,
    string? ActiveProjectId,
    string? ActiveProjectName,
    IReadOnlyCollection<McpOrganizationResult> Organizations,
    IReadOnlyCollection<McpProjectResult> Projects,
    bool RequiresOrganizationSelection,
    bool RequiresProjectSelection,
    DateTime? UpdatedUtc = null)
{
    public static McpContextResult Empty { get; } = new(null, null, null, null, [], [], false, false);
}

public sealed record McpOrganizationResult(
    string Id,
    string Name,
    string Url);

public sealed record McpTimeRange(DateTime? StartUtc, DateTime? EndUtc)
{
    public bool HasRange => StartUtc.HasValue || EndUtc.HasValue;
}

public sealed record McpFilterFieldsResult(
    McpFilterFieldSet Projects,
    McpFilterFieldSet Stacks,
    McpFilterFieldSet Events);

public sealed record McpFilterFieldSet(
    IReadOnlyCollection<string> FilterFields,
    IReadOnlyCollection<string> SortFields,
    IReadOnlyCollection<string> DynamicFilterPrefixes);

public sealed record McpEventCountResult(
    long Events,
    double Occurrences,
    long Stacks,
    long Users,
    IReadOnlyCollection<McpEventTrendBucket> Trend,
    string? Interval = null,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    string? GroupBy = null,
    IReadOnlyCollection<McpEventCountGroup>? Groups = null);

public sealed record McpEventTrendBucket(
    string Date,
    long Events,
    double Occurrences);

public sealed record McpEventCountGroup(
    string Key,
    long Events,
    double Occurrences,
    IReadOnlyCollection<McpEventTrendBucket> Trend);

public sealed record McpProjectResult(
    string Id,
    string OrganizationId,
    string Name,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url,
    bool? IsConfigured = null,
    DateTime? LastEventDateUtc = null);

public sealed record McpClientSetupInstructionsResult(
    string ProjectId,
    string ProjectName,
    string Platform,
    string PackageName,
    string ApiKey,
    bool HasApiKey,
    string DocumentationUrl,
    IReadOnlyCollection<McpClientSetupStep> Steps,
    IReadOnlyCollection<string> Notes);

public sealed record McpClientSetupStep(
    string Title,
    string Instructions,
    string? Command = null,
    string? Code = null,
    string? Language = null);

public sealed record McpStackResult(
    string Id,
    string OrganizationId,
    string ProjectId,
    string Type,
    string Status,
    string Title,
    int TotalOccurrences,
    DateTime FirstOccurrence,
    DateTime LastOccurrence,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> References,
    bool OccurrencesAreCritical,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url,
    string? Description = null,
    DateTime? DateFixed = null,
    string? FixedInVersion = null,
    DateTime? SnoozeUntilUtc = null);

public sealed record McpStackUpdateResult(
    McpStackResult Stack,
    bool Changed,
    string Message);

public sealed record McpEventResult(
    string Id,
    string OrganizationId,
    string ProjectId,
    string StackId,
    DateTimeOffset Date,
    IReadOnlyCollection<string> Tags,
    bool IsFirstOccurrence,
    DateTime CreatedUtc,
    string Url,
    string? Type = null,
    string? Source = null,
    string? Message = null,
    string? ReferenceId = null,
    McpEventDetails? Details = null);

public sealed record McpEventDetails(
    bool IsTruncated = false,
    int? Size = null,
    int? MaxSize = null,
    string? TruncationMessage = null,
    object? Error = null,
    RequestInfo? Request = null,
    EnvironmentInfo? Environment = null,
    DataDictionary? Data = null);
