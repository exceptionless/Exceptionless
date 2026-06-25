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

public sealed record McpPagination(bool HasMore, string? Before, string? After, int Limit);

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
    string? Interval,
    DateTime? StartUtc,
    DateTime? EndUtc,
    IReadOnlyCollection<McpEventTrendBucket> Trend,
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
    bool? IsConfigured,
    DateTime? LastEventDateUtc,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url);

public sealed record McpStackResult(
    string Id,
    string OrganizationId,
    string ProjectId,
    string Type,
    string Status,
    string Title,
    string? Description,
    int TotalOccurrences,
    DateTime FirstOccurrence,
    DateTime LastOccurrence,
    DateTime? DateFixed,
    string? FixedInVersion,
    DateTime? SnoozeUntilUtc,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> References,
    bool OccurrencesAreCritical,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url);

public sealed record McpStackUpdateResult(
    McpStackResult Stack,
    bool Changed,
    string Message);

public sealed record McpEventResult(
    string Id,
    string OrganizationId,
    string ProjectId,
    string StackId,
    string? Type,
    string? Source,
    string? Message,
    DateTimeOffset Date,
    IReadOnlyCollection<string> Tags,
    string? ReferenceId,
    bool IsFirstOccurrence,
    DateTime CreatedUtc,
    string Url,
    McpEventDetails? Details = null);

public sealed record McpEventDetails(
    object? Error,
    RequestInfo? Request,
    EnvironmentInfo? Environment,
    DataDictionary? Data,
    bool IsTruncated = false,
    int? Size = null,
    int? MaxSize = null,
    string? TruncationMessage = null);
