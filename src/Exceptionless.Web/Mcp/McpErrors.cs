using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Web.Mcp;

public static class McpErrorCodes
{
    public const string Forbidden = "forbidden";
    public const string InvalidCursor = "invalid_cursor";
    public const string InvalidDetailSize = "invalid_detail_size";
    public const string InvalidFilter = "invalid_filter";
    public const string InvalidGroupBy = "invalid_group_by";
    public const string InvalidId = "invalid_id";
    public const string InvalidInterval = "invalid_interval";
    public const string InvalidLimit = "invalid_limit";
    public const string InvalidReferenceLink = "invalid_reference_link";
    public const string InvalidSnooze = "invalid_snooze";
    public const string InvalidSort = "invalid_sort";
    public const string InvalidStatus = "invalid_status";
    public const string InvalidTimeRange = "invalid_time_range";
    public const string InvalidVersion = "invalid_version";
    public const string NotAccessible = "not_accessible";
    public const string NotFound = "not_found";
    public const string QueryFailed = "query_failed";
    public const string UnknownFilterField = "unknown_filter_field";
}

public static class McpErrors
{
    public static McpErrorInfo Forbidden(string message, string requiredScope)
    {
        return new McpErrorInfo(McpErrorCodes.Forbidden, message, new Dictionary<string, object?>
        {
            ["requiredScope"] = requiredScope
        });
    }

    public static McpErrorInfo InvalidCursor(string message, string field)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidCursor, message, new Dictionary<string, object?>
        {
            ["field"] = field
        });
    }

    public static McpErrorInfo InvalidDetailSize(string message, int value, int min, int max)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidDetailSize, message, new Dictionary<string, object?>
        {
            ["value"] = value,
            ["min"] = min,
            ["max"] = max
        });
    }

    public static McpErrorInfo InvalidFilter(string message)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidFilter, message);
    }

    public static McpErrorInfo InvalidGroupBy(string message, string? groupBy, IEnumerable<string> allowedFields)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidGroupBy, message, new Dictionary<string, object?>
        {
            ["groupBy"] = groupBy,
            ["allowedFields"] = allowedFields.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    public static McpErrorInfo InvalidId(string message, string field, string? value)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidId, message, new Dictionary<string, object?>
        {
            ["field"] = field,
            ["value"] = value
        });
    }

    public static McpErrorInfo InvalidInterval(string message, string? interval)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidInterval, message, new Dictionary<string, object?>
        {
            ["interval"] = interval
        });
    }

    public static McpErrorInfo InvalidLimit(string message, int value, int max)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidLimit, message, new Dictionary<string, object?>
        {
            ["value"] = value,
            ["min"] = 1,
            ["max"] = max
        });
    }

    public static McpErrorInfo InvalidReferenceLink(string message, string? url)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidReferenceLink, message, new Dictionary<string, object?>
        {
            ["field"] = "url",
            ["value"] = url
        });
    }

    public static McpErrorInfo InvalidSnooze(string message, string? duration, string? snoozeUntilUtc)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidSnooze, message, new Dictionary<string, object?>
        {
            ["duration"] = duration,
            ["snoozeUntilUtc"] = snoozeUntilUtc
        });
    }

    public static McpErrorInfo InvalidSort(string message, string? sort, IReadOnlySet<string> allowedFields)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidSort, message, new Dictionary<string, object?>
        {
            ["sort"] = sort,
            ["allowedFields"] = allowedFields.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    public static McpErrorInfo InvalidStatus(string message, string? status)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidStatus, message, new Dictionary<string, object?>
        {
            ["status"] = status,
            ["allowedStatuses"] = new[] { "open", "fixed", "ignored", "discarded" }
        });
    }

    public static McpErrorInfo InvalidTimeRange(string message, string? last, string? startUtc, string? endUtc)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidTimeRange, message, new Dictionary<string, object?>
        {
            ["last"] = last,
            ["startUtc"] = startUtc,
            ["endUtc"] = endUtc
        });
    }

    public static McpErrorInfo InvalidVersion(string message, string? fixedInVersion)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidVersion, message, new Dictionary<string, object?>
        {
            ["fixedInVersion"] = fixedInVersion
        });
    }

    public static McpErrorInfo NotAccessible(string message, string? resource = null, string? id = null)
    {
        return new McpErrorInfo(McpErrorCodes.NotAccessible, message, ResourceDetails(resource, id));
    }

    public static McpErrorInfo NotFound(string message, string? field = null, string? value = null)
    {
        return new McpErrorInfo(McpErrorCodes.NotFound, message, ResourceDetails(field, value));
    }

    public static McpErrorInfo QueryFailed(string message)
    {
        return new McpErrorInfo(McpErrorCodes.QueryFailed, message);
    }

    public static McpErrorInfo UnknownFilterField(string message, string field, IReadOnlySet<string> allowedFields)
    {
        return new McpErrorInfo(McpErrorCodes.UnknownFilterField, message, new Dictionary<string, object?>
        {
            ["field"] = field,
            ["allowedFields"] = allowedFields.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    private static IReadOnlyDictionary<string, object?>? ResourceDetails(string? field, string? value)
    {
        if (String.IsNullOrEmpty(field) && String.IsNullOrEmpty(value))
            return null;

        return new Dictionary<string, object?>
        {
            ["field"] = field,
            ["value"] = value
        };
    }
}

public sealed record McpErrorInfo(string Code, string Message, IReadOnlyDictionary<string, object?>? Details = null);

public sealed class McpForbiddenException(string message, string requiredScope) : UnauthorizedAccessException(message)
{
    public string RequiredScope { get; } = requiredScope;
}
