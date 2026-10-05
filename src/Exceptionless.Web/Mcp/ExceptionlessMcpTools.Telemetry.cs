using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Web.Api.Infrastructure;
using ModelContextProtocol.Server;

namespace Exceptionless.Web.Mcp;

public sealed partial class ExceptionlessMcpTools
{
    private const string TelemetryTimeDescription = "Optional relative time range such as 24h or 7d. Defaults to the last 24 hours when no time is supplied. Do not combine with startUtc or endUtc. With only one absolute bound, end defaults to now and start defaults to 24 hours before end. Retention still applies.";

    [McpServerTool(Name = "get_event_measurements", Title = "Discover event measurements", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Discovers native measurement names and exact, case-sensitive units in a project's filtered events. Use before sorting numeric observations or calling get_event_chart. Examples: duration/s, allocated/By, sql.calls/{call}. Up to 100 names and 20 units per name; truncated=true means discovery is incomplete and the filter/time window should be narrowed. Requires events:read and the same plan access as the chart API.")]
    public Task<McpResponse<EventMeasurementCatalog>> GetEventMeasurementsAsync(
        [Description("Optional Exceptionless project id. May be omitted when only one project is accessible.")]
        string? projectId = null,
        [Description(EventFilterDescription)]
        string? filter = null,
        [Description(TelemetryTimeDescription)]
        string? last = null,
        [Description(StartUtcDescription)]
        string? startUtc = null,
        [Description(EndUtcDescription)]
        string? endUtc = null)
        => RunTelemetryQueryAsync(projectId, filter, last, startUtc, endUtc,
            (scope, start, end) => _eventTelemetryService.GetMeasurementsAsync(scope, filter, start, end));

    [McpServerTool(Name = "get_event_chart", Title = "Query event measurement history", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Queries measurement history or event counts using the same bounded aggregation engine as saved-view charts. Select an exact measurement/unit (no conversion), filter by stack/source for one operation across runs, or parent/root references for one execution. Group by source, stack, outcome, result, or labels.<name>. Compare duration, allocations, or call counts across equivalent environments. Bucket mode returns at most 20 series and about 200 UTC buckets; event mode returns the latest 1000 observations with event IDs. Series are selected by frequency, not slowness: use search_events with -measurement.duration@s for slowest observations. Honor truncated=true and narrow the query or paginate search_events for a complete history. Missing values are not zero; percentiles describe submitted observations, not underlying samples of benchmark means. Requires events:read and chart plan access.")]
    public Task<McpResponse<EventChartResult>> GetEventChartAsync(
        [Description("Optional Exceptionless project id. May be omitted when only one project is accessible.")]
        string? projectId = null,
        [Description("Native measurement name discovered with get_event_measurements, such as duration or allocated. Omit with unit and choose aggregation=count to count events in buckets.")]
        string? measurement = null,
        [Description("Exact case-sensitive unit, such as s, ms, By, {call}, or {request}/s. Required with measurement. Units are not converted.")]
        string? unit = null,
        [Description("Aggregation of submitted observations. Event mode uses recorded values. Count counts observations, not the legacy duplicate count field.")]
        [AllowedValues("avg", "min", "max", "sum", "p50", "p95", "p99", "count")]
        string aggregation = "avg",
        [Description("Optional series split: source, stack, outcome, result, or labels.<name>. Missing split values are excluded from bucketed series.")]
        string? groupBy = null,
        [Description("buckets for a time series; events for individual observations with event IDs.")]
        [AllowedValues("buckets", "events")]
        string mode = "buckets",
        [Description(EventFilterDescription)]
        string? filter = null,
        [Description(TelemetryTimeDescription)]
        string? last = null,
        [Description(StartUtcDescription)]
        string? startUtc = null,
        [Description(EndUtcDescription)]
        string? endUtc = null)
    {
        var chart = new EventChart { Measurement = measurement, Unit = unit, Aggregation = aggregation, GroupBy = groupBy, Mode = mode };
        return RunTelemetryQueryAsync(projectId, filter, last, startUtc, endUtc,
            (scope, start, end) => _eventTelemetryService.GetChartAsync(scope, chart, filter, start, end), chart);
    }

    private async Task<McpResponse<T>> RunTelemetryQueryAsync<T>(string? projectId, string? filter, string? last, string? startUtc, string? endUtc,
        Func<AppFilter, DateTime, DateTime, Task<T>> query, EventChart? chart = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            if (projectId is not null && !TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<T>.Failed(idError);

            if (chart is not null)
            {
                var errors = new List<ValidationResult>();
                if (!Validator.TryValidateObject(chart, new ValidationContext(chart), errors, validateAllProperties: true))
                    return McpResponse<T>.Failed(new McpErrorInfo(McpErrorCodes.InvalidChart, String.Join(" ", errors.Select(e => e.ErrorMessage))));
            }

            var validation = await ValidateSearchAsync(filter, sort: null, DefaultLimit, EventFilterFields, EventSortFields, _eventQueryValidator, allowEventFields: true);
            if (validation.Error is not null)
                return McpResponse<T>.Failed(validation.Error);
            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<T>.Failed(timeError);

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<T>.Failed(projectContext.Error!);

            var organization = projectContext.Organization!;
            if (organization.IsSuspended)
                return McpResponse<T>.Failed(McpErrors.NotAccessible("Unable to view events for the suspended organization."));

            var scope = new AppFilter(projectContext.Project!, organization) { UsesPremiumFeatures = true };
            if (ApiFilterPolicy.IsPremiumFeatureQueryBlocked(scope))
                return McpResponse<T>.Failed(new McpErrorInfo(McpErrorCodes.PlanLimit, ApiFilterPolicy.PremiumSearchUpgradeMessage));

            DateTime end = timeRange.EndUtc ?? _timeProvider.GetUtcNow().UtcDateTime;
            DateTime start = timeRange.StartUtc ?? end.AddDays(-1);
            if (start >= end)
                return McpResponse<T>.Failed(McpErrors.InvalidTimeRange("startUtc must be before endUtc (defaults to now).", last, startUtc, endUtc));

            var result = await query(scope, start, end);
            bool truncated = result is EventChartResult { Truncated: true } or EventMeasurementCatalog { Truncated: true };
            return McpResponse<T>.Success(result, truncated ? "Results are incomplete. Narrow the filter/time range, or paginate search_events for complete observations." : null);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<T>.Failed(ToLookupError("Project", projectId ?? "current authorization", ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<T>.Failed(McpErrors.QueryFailed("Unable to query event telemetry. Check the filter, measurement, unit, and time range."));
        }
    }

    private static bool IsLabelField(string field) => field.StartsWith("labels.", StringComparison.Ordinal)
        && Regex.IsMatch(field["labels.".Length..], EventMeasurement.NamePattern);
}
