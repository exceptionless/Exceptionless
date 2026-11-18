using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Mediator;
using Foundatio.Repositories;
using MiniValidation;
using ModelContextProtocol.Server;

namespace Exceptionless.Web.Mcp;

/// <summary>
/// Saved view tools. Changes go through <see cref="Api.Handlers.SavedViewHandler"/>, so they follow the same rules as the app.
/// </summary>
public sealed partial class ExceptionlessMcpTools
{
    private const string DefaultSavedViewTime = "[now-7d TO now]";
    private const string DefaultSavedViewSort = "-date";
    private const int MaximumListedSavedViews = 50;
    private const int MaximumSavedViewPages = 3;

    private const string SavedViewProjectIdsDescription = "Project ids to limit the view to. Use list_projects to find a project id by name.";
    private const string SavedViewTypesDescription = "Event types to include, such as error, 404, log, or usage. Not available for sessions views.";
    private const string SavedViewEnvironmentsDescription = "Environments to include, such as production.";
    private const string SavedViewLevelsDescription = "Log levels to include: trace, debug, info, warn, error, or fatal.";
    private const string SavedViewTagsDescription = "Tags to include. The view matches events with any of them.";
    private const string SavedViewQueryDescription = "Exceptionless search for anything the other arguments cannot express, such as critical:true for stacks marked critical, error.type:\"System.TimeoutException\", or user.email:jane@example.com. Unknown fields are rejected.";
    private const string SavedViewTimeDescription = "Date range the view shows, in date math such as [now-24h TO now] or [now-30d TO now]. Not available for stream views.";
    private const string SavedViewSortDescription = "Sort for events and sessions views: -date for newest first or date for oldest first. Stacks views are ranked by frequency and stream views show newest first, so neither can be sorted.";

    private static readonly string[] s_defaultSavedViewStatuses = [Stack.KnownStatuses.Open, Stack.KnownStatuses.Regressed];
    private static readonly string[] s_savedViewStatuses = [Stack.KnownStatuses.Open, Stack.KnownStatuses.Regressed, Stack.KnownStatuses.Fixed, Stack.KnownStatuses.Snoozed, Stack.KnownStatuses.Ignored, Stack.KnownStatuses.Discarded];
    private static readonly string[] s_savedViewLevels = ["trace", "debug", "info", "warn", "error", "fatal", "off"];
    private static readonly HashSet<string> s_savedViewSorts = new(StringComparer.Ordinal) { DefaultSavedViewSort, "date" };
    private static readonly string[] s_clearableSavedViewCriteria = ["type", "status", "environment", "level", "tag", "query"];

    /// <summary>A view's filter runs as an event search, which also accepts the stack fields its stack filter resolves.</summary>
    private static readonly HashSet<string> SavedViewFilterFields = new(EventFilterFields, StringComparer.OrdinalIgnoreCase)
    {
        "_missing_",
        "_exists_",
        StackIndex.Alias.LastOccurrence, "last_occurrence",
        "first_occurrence",
        StackIndex.Alias.References, "references",
        "snooze_until_utc",
        StackIndex.Alias.SignatureHash, "signature_hash",
        StackIndex.Alias.Title, "title",
        StackIndex.Alias.Description, "description",
        StackIndex.Alias.DateFixed, "date_fixed",
        StackIndex.Alias.FixedInVersion, "fixed_in_version",
        StackIndex.Alias.OccurrencesAreCritical, "occurrences_are_critical",
        StackIndex.Alias.TotalOccurrences, "total_occurrences",
        StackIndex.Alias.IsFixed, "is_fixed",
        StackIndex.Alias.IsRegressed, "is_regressed",
        StackIndex.Alias.IsHidden, "is_hidden"
    };

    // The app writes event types into the filter unquoted.
    [GeneratedRegex(@"^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SavedViewEventTypeRegex();

    [McpServerTool(Name = "list_saved_views", Title = "List saved views", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists the saved views the current user can see in an organization. Call this to find a view's id before changing or deleting it.")]
    public async Task<McpResponse<McpListData<McpSavedViewResult>>> ListSavedViewsAsync(
        [Description("Optional Exceptionless organization id. May be omitted when only one organization is accessible.")]
        string? organizationId = null,
        [Description("Optional page to list views for: stacks, events, sessions, or stream.")]
        string? viewType = null,
        [Description("Optional view id, or text the view's name or URL name must contain, ignoring case. A view whose id, name, or URL name matches exactly is returned on its own.")]
        string? name = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.SavedViewsRead);
            viewType = NullIfBlank(viewType);
            name = NullIfBlank(name);
            var (resolvedOrganizationId, organizationError) = await ResolveSavedViewOrganizationAsync(organizationId);
            if (resolvedOrganizationId is null)
                return McpResponse<McpListData<McpSavedViewResult>>.Failed(organizationError!);

            var views = new List<ViewSavedView>();
            for (int page = 1; page <= MaximumSavedViewPages; page++)
            {
                var result = await _mediator.InvokeAsync<Result<PagedResult<ViewSavedView>>>(new GetSavedViewsByOrganization(resolvedOrganizationId, page, Pagination.MaximumLimit));
                if (!result.IsSuccess)
                    return McpResponse<McpListData<McpSavedViewResult>>.Failed(ToSavedViewError(result, "Unable to list saved views."));

                views.AddRange(result.Value.Items);
                if (!result.Value.HasMore)
                    break;
            }

            var pageViews = views.Where(view => viewType is null || String.Equals(view.ViewType, viewType, StringComparison.OrdinalIgnoreCase)).ToList();
            // "Production errors" names one view even when "Production errors (7 days)" also contains it.
            var exactMatches = name is null ? [] : pageViews.Where(view => IsExactSavedViewMatch(view, name)).ToList();
            var matches = (exactMatches.Count > 0 ? exactMatches : pageViews.Where(view => name is null || view.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || view.Slug.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(view => view.ViewType, StringComparer.Ordinal)
                .ThenBy(view => view.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = matches.Take(MaximumListedSavedViews).Select(view => ToSavedViewResult(view)).ToList();
            return McpResponse<McpListData<McpSavedViewResult>>.Success(
                new McpListData<McpSavedViewResult>(items, matches.Count),
                matches.Count > items.Count ? $"Showing {items.Count} of {matches.Count} views. Pass name or viewType to narrow the list." : null);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpListData<McpSavedViewResult>>.Failed(ToLookupError("Organization", organizationId ?? "current authorization", ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpListData<McpSavedViewResult>>.Failed(McpErrors.QueryFailed("Unable to list saved views. Check the organization id."));
        }
    }

    [McpServerTool(Name = "create_saved_view", Title = "Create saved view", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates a saved view in the sidebar with its own project, filters, time range, and sort. Only the current user can see it unless shared is true.")]
    public async Task<McpResponse<McpSavedViewResult>> CreateSavedViewAsync(
        [Description("The name shown in the sidebar. It must be unique on its page.")]
        string name,
        [Description("Which page the view belongs to: stacks for grouped errors and issues, events for individual events, logs, or feature usage, sessions for user sessions, or stream for the live event stream.")]
        [AllowedValues("stacks", "events", "sessions", "stream")]
        string viewType,
        [Description("Optional Exceptionless organization id. May be omitted when only one organization is accessible.")]
        string? organizationId = null,
        [Description(SavedViewProjectIdsDescription)]
        string[]? projectIds = null,
        [Description("Set to true to include every project, overriding projectIds. Either projectIds or this is required.")]
        bool allProjects = false,
        [Description(SavedViewTypesDescription)]
        string[]? types = null,
        [Description("Stack statuses to include: open, regressed, fixed, snoozed, ignored, or discarded. Defaults to open and regressed, except on sessions views. List all six to include every status.")]
        string[]? statuses = null,
        [Description(SavedViewEnvironmentsDescription)]
        string[]? environments = null,
        [Description(SavedViewLevelsDescription)]
        string[]? levels = null,
        [Description(SavedViewTagsDescription)]
        string[]? tags = null,
        [Description(SavedViewQueryDescription)]
        string? query = null,
        [Description(SavedViewTimeDescription + " Defaults to the last 7 days.")]
        string? time = null,
        [Description(SavedViewSortDescription + " Defaults to -date.")]
        string? sort = null,
        [Description("Set to true only when the user asks for everyone in the organization to see the view. This cannot be changed after the view is created.")]
        bool shared = false)
    {
        try
        {
            EnsureScope(AuthorizationRoles.SavedViewsWrite);
            time = NullIfBlank(time);
            sort = NullIfBlank(sort);
            var (resolvedOrganizationId, organizationError) = await ResolveSavedViewOrganizationAsync(organizationId);
            if (resolvedOrganizationId is null)
                return McpResponse<McpSavedViewResult>.Failed(organizationError!);

            string normalizedViewType = viewType?.Trim().ToLowerInvariant() ?? String.Empty;
            if (!NewSavedView.ValidViewTypes.Contains(normalizedViewType))
                return McpResponse<McpSavedViewResult>.Failed(InvalidSavedViewArgument($"viewType must be one of: {String.Join(", ", NewSavedView.ValidViewTypes)}."));

            var displayError = ValidateSavedViewDisplay(normalizedViewType, types, time, sort);
            if (displayError is not null)
                return McpResponse<McpSavedViewResult>.Failed(displayError);

            // Leaving out the projects would quietly include every one, which is rarely what was meant.
            if (!allProjects && projectIds?.Any(projectId => !String.IsNullOrWhiteSpace(projectId)) is not true)
                return McpResponse<McpSavedViewResult>.Failed(InvalidSavedViewArgument("Pass projectIds with the projects the view is for, or allProjects as true to include every project."));

            var definitions = new JsonArray();
            SetSavedViewProjects(definitions, []);

            // Like the built-in views, new views leave out what is no longer active unless asked otherwise.
            bool usesDefaultStatuses = statuses is not { Length: > 0 } && normalizedViewType != "sessions";
            var criteriaError = await ApplySavedViewCriteriaAsync(
                definitions,
                resolvedOrganizationId,
                allProjects ? null : projectIds,
                types,
                usesDefaultStatuses ? null : statuses,
                environments,
                levels,
                tags,
                query);
            if (criteriaError is not null)
                return McpResponse<McpSavedViewResult>.Failed(criteriaError);

            if (usesDefaultStatuses)
                SavedViewFilterDefinitions.Set(definitions, "status", ToJsonArray(s_defaultSavedViewStatuses), hidden: true);

            string? resolvedTime = normalizedViewType == "stream" ? null : time ?? DefaultSavedViewTime;
            if (resolvedTime is not null)
                SavedViewFilterDefinitions.Set(definitions, "date", JsonValue.Create(resolvedTime), term: "date");

            string? filter = SavedViewFilterDefinitions.ToFilter(definitions);
            var filterError = await ValidateFilterExpressionAsync(filter, _eventQueryValidator);
            if (filterError is not null)
                return McpResponse<McpSavedViewResult>.Failed(filterError);

            var savedView = new NewSavedView
            {
                OrganizationId = resolvedOrganizationId,
                Name = name?.Trim() ?? String.Empty,
                ViewType = normalizedViewType,
                Filter = filter,
                FilterDefinitions = definitions.ToJsonString(),
                Time = resolvedTime,
                Sort = normalizedViewType is "events" or "sessions" ? sort ?? DefaultSavedViewSort : null,
                IsPrivate = !shared
            };

            (bool isValid, var errors) = await MiniValidator.TryValidateAsync(savedView, recurse: false);
            if (!isValid)
                return McpResponse<McpSavedViewResult>.Failed(InvalidSavedViewArgument(errors.SelectMany(error => error.Value).FirstOrDefault() ?? "The saved view is not valid."));

            var result = await _mediator.InvokeAsync<Result<ViewSavedView>>(new CreateSavedView(resolvedOrganizationId, savedView));
            if (!result.IsSuccess)
                return McpResponse<McpSavedViewResult>.Failed(ToSavedViewError(result, "Unable to create the saved view."));

            return McpResponse<McpSavedViewResult>.Success(ToSavedViewResult(result.Value, changed: true, $"Saved view '{result.Value.Name}' was created."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpSavedViewResult>.Failed(ToLookupError("Organization", organizationId ?? "current authorization", ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpSavedViewResult>.Failed(McpErrors.QueryFailed("Unable to create the saved view. Check the name, filters, and time range."));
        }
    }

    [McpServerTool(Name = "update_saved_view", Title = "Update saved view", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Renames a saved view or changes its filters, time range, or sort. Only pass what should change. Each criteria argument replaces that criteria, and everything else on the view is kept. Use the id from list_saved_views. Who can see a view cannot be changed.")]
    public async Task<McpResponse<McpSavedViewResult>> UpdateSavedViewAsync(
        [Description("The saved view id from list_saved_views.")]
        string savedViewId,
        [Description("A new name for the sidebar. The view's URL stays the same so existing links keep working.")]
        string? name = null,
        [Description(SavedViewProjectIdsDescription)]
        string[]? projectIds = null,
        [Description("Set to true to stop limiting the view to specific projects.")]
        bool allProjects = false,
        [Description(SavedViewTypesDescription)]
        string[]? types = null,
        [Description("Stack statuses to include: open, regressed, fixed, snoozed, ignored, or discarded.")]
        string[]? statuses = null,
        [Description(SavedViewEnvironmentsDescription)]
        string[]? environments = null,
        [Description(SavedViewLevelsDescription)]
        string[]? levels = null,
        [Description(SavedViewTagsDescription)]
        string[]? tags = null,
        [Description(SavedViewQueryDescription)]
        string? query = null,
        [Description("Criteria to remove from the view: type, status, environment, level, tag, or query. Removing status includes every status.")]
        string[]? clearCriteria = null,
        [Description(SavedViewTimeDescription)]
        string? time = null,
        [Description(SavedViewSortDescription)]
        string? sort = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.SavedViewsWrite);
            name = NullIfBlank(name);
            time = NullIfBlank(time);
            sort = NullIfBlank(sort);
            var (view, viewError) = await GetSavedViewAsync(savedViewId);
            if (view is null)
                return McpResponse<McpSavedViewResult>.Failed(viewError!);

            var displayError = ValidateSavedViewDisplay(view.ViewType, types, time, sort);
            if (displayError is not null)
                return McpResponse<McpSavedViewResult>.Failed(displayError);

            // The whole custom filter is one search, so changing a criteria or the search alone would combine with or
            // silently replace the rest of it. Replacing it has to be asked for.
            if (SavedViewFilterDefinitions.IsRawFilter(view.FilterDefinitions, view.Filter)
                && (ChangesSeparateCriteria(projectIds, allProjects, types, statuses, environments, levels, tags, clearCriteria) || (NullIfBlank(query) is not null && !ClearsQuery(clearCriteria))))
                return McpResponse<McpSavedViewResult>.Failed(InvalidSavedViewArgument($"This view uses a custom filter, so its criteria cannot be changed one at a time: {view.Filter}. To replace the whole filter, pass query with the complete new filter and clearCriteria [\"query\"]."));

            var definitions = SavedViewFilterDefinitions.Parse(view.FilterDefinitions, view.Filter);
            string originalDefinitions = definitions.ToJsonString();

            foreach (string criteria in clearCriteria ?? [])
            {
                string normalizedCriteria = criteria.Trim().ToLowerInvariant();
                if (!s_clearableSavedViewCriteria.Contains(normalizedCriteria))
                    return McpResponse<McpSavedViewResult>.Failed(InvalidSavedViewArgument($"clearCriteria values must be: {String.Join(", ", s_clearableSavedViewCriteria)}."));

                SavedViewFilterDefinitions.Remove(definitions, normalizedCriteria == "query" ? "keyword" : normalizedCriteria);
            }

            if (allProjects)
                SetSavedViewProjects(definitions, []);

            var criteriaError = await ApplySavedViewCriteriaAsync(definitions, view.OrganizationId, allProjects ? null : projectIds, types, statuses, environments, levels, tags, query);
            if (criteriaError is not null)
                return McpResponse<McpSavedViewResult>.Failed(criteriaError);

            if (time is not null)
                SavedViewFilterDefinitions.Set(definitions, "date", JsonValue.Create(time), term: "date");

            var changes = new Delta<UpdateSavedView>();
            if (name is not null && !String.Equals(name, view.Name, StringComparison.Ordinal))
                changes.TrySetPropertyValue(nameof(UpdateSavedView.Name), name);

            if (definitions.ToJsonString() != originalDefinitions)
            {
                string? filter = SavedViewFilterDefinitions.ToFilter(definitions);
                var filterError = await ValidateFilterExpressionAsync(filter, _eventQueryValidator);
                if (filterError is not null)
                    return McpResponse<McpSavedViewResult>.Failed(filterError);

                // Patching skips nulls, so an empty filter is what removes the last criteria.
                changes.TrySetPropertyValue(nameof(UpdateSavedView.Filter), filter ?? String.Empty);
                changes.TrySetPropertyValue(nameof(UpdateSavedView.FilterDefinitions), definitions.ToJsonString());
            }

            if (time is not null && !String.Equals(time, view.Time, StringComparison.Ordinal))
                changes.TrySetPropertyValue(nameof(UpdateSavedView.Time), time);

            if (sort is not null && !String.Equals(sort, view.Sort, StringComparison.Ordinal))
                changes.TrySetPropertyValue(nameof(UpdateSavedView.Sort), sort);

            if (!changes.GetChangedPropertyNames().Any())
                return McpResponse<McpSavedViewResult>.Success(ToSavedViewResult(view, changed: false, $"Saved view '{view.Name}' already matches."));

            var result = await _mediator.InvokeAsync<Result<ViewSavedView>>(new UpdateSavedViewMessage(view.Id, changes));
            if (!result.IsSuccess)
                return McpResponse<McpSavedViewResult>.Failed(ToSavedViewError(result, "Unable to update the saved view."));

            return McpResponse<McpSavedViewResult>.Success(ToSavedViewResult(result.Value, changed: true, $"Saved view '{result.Value.Name}' was updated."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpSavedViewResult>.Failed(ToLookupError("Saved view", savedViewId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpSavedViewResult>.Failed(McpErrors.QueryFailed("Unable to update the saved view. Check the id, filters, and time range."));
        }
    }

    [McpServerTool(Name = "delete_saved_view", Title = "Delete saved view", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Permanently deletes a saved view. A shared view is deleted for everyone in the organization. Use the id from list_saved_views.")]
    public async Task<McpResponse<McpSavedViewResult>> DeleteSavedViewAsync(
        [Description("The saved view id from list_saved_views.")]
        string savedViewId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.SavedViewsWrite);
            var (view, viewError) = await GetSavedViewAsync(savedViewId);
            if (view is null)
                return McpResponse<McpSavedViewResult>.Failed(viewError!);

            var result = await _mediator.InvokeAsync<Result<ModelActionResults>>(new DeleteSavedViews([view.Id]));
            if (!result.IsSuccess)
                return McpResponse<McpSavedViewResult>.Failed(ToSavedViewError(result, "Unable to delete the saved view."));

            return McpResponse<McpSavedViewResult>.Success(ToSavedViewResult(view, changed: true, $"Saved view '{view.Name}' was deleted."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpSavedViewResult>.Failed(ToLookupError("Saved view", savedViewId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpSavedViewResult>.Failed(McpErrors.QueryFailed("Unable to delete the saved view. Check the id."));
        }
    }

    private async Task<(string? OrganizationId, McpErrorInfo? Error)> ResolveSavedViewOrganizationAsync(string? organizationId)
    {
        if (organizationId is not null && !TryValidateId(organizationId, "organizationId", out var idError))
            return (null, idError);

        var context = await _mcpContextService.GetContextAsync(organizationId: organizationId, requireProject: false);
        if (!context.Succeeded)
            return (null, context.Error);

        var organization = context.ActiveOrganization ?? throw new UnauthorizedAccessException("The requested organization is not available.");
        return (organization.Id, null);
    }

    private async Task<(ViewSavedView? View, McpErrorInfo? Error)> GetSavedViewAsync(string savedViewId)
    {
        if (!TryValidateId(savedViewId, "savedViewId", out var idError))
            return (null, idError);

        var result = await _mediator.InvokeAsync<Result<ViewSavedView>>(new GetSavedViewById(savedViewId));
        if (!result.IsSuccess)
            return (null, ToSavedViewError(result, "Saved view not found."));

        EnsureOrganizationAccess(result.Value.OrganizationId);
        return (result.Value, null);
    }

    /// <summary>True when everyone in the organization sees the view, so changing it changes it for all of them.</summary>
    internal async Task<bool> IsSharedSavedViewAsync(string savedViewId)
    {
        try
        {
            var (view, _) = await GetSavedViewAsync(savedViewId);
            return view is { UserId: null };
        }
        catch (Exception ex) when (IsLookupError(ex) || IsExpectedToolError(ex))
        {
            return false;
        }
    }

    private async Task<McpErrorInfo?> ApplySavedViewCriteriaAsync(
        JsonArray definitions,
        string organizationId,
        string[]? projectIds,
        string[]? types,
        string[]? statuses,
        string[]? environments,
        string[]? levels,
        string[]? tags,
        string? query)
    {
        // An empty value means unchanged, because strict tool calling fills optional arguments with them.
        if (projectIds is { Length: > 0 })
        {
            var projectError = await ValidateSavedViewProjectsAsync(organizationId, projectIds);
            if (projectError is not null)
                return projectError;

            SetSavedViewProjects(definitions, projectIds);
        }

        if (types is { Length: > 0 })
        {
            if (types.FirstOrDefault(type => !SavedViewEventTypeRegex().IsMatch(type.Trim())) is { } invalidType)
                return InvalidSavedViewArgument($"'{invalidType}' is not a valid event type.");

            SavedViewFilterDefinitions.Set(definitions, "type", ToJsonArray(types.Select(type => type.Trim())));
        }

        if (statuses is { Length: > 0 })
        {
            string[] normalizedStatuses = statuses.Select(status => status.Trim().ToLowerInvariant()).ToArray();
            if (normalizedStatuses.FirstOrDefault(status => !s_savedViewStatuses.Contains(status)) is { } invalidStatus)
                return McpErrors.InvalidStatus($"Status must be one of: {String.Join(", ", s_savedViewStatuses)}.", invalidStatus);

            SavedViewFilterDefinitions.Set(definitions, "status", ToJsonArray(normalizedStatuses));
        }

        if (environments is { Length: > 0 })
        {
            string[] normalizedEnvironments = environments.Select(environment => environment.Trim().ToLowerInvariant()).ToArray();
            if (normalizedEnvironments.FirstOrDefault(environment => !IsPlainSavedViewValue(environment)) is { } invalidEnvironment)
                return InvalidSavedViewArgument($"'{invalidEnvironment}' is not a valid environment.");

            SavedViewFilterDefinitions.Set(definitions, "environment", ToJsonArray(normalizedEnvironments));
        }

        if (levels is { Length: > 0 })
        {
            string[] normalizedLevels = levels.Select(level => level.Trim().ToLowerInvariant()).ToArray();
            if (normalizedLevels.FirstOrDefault(level => !s_savedViewLevels.Contains(level)) is { } invalidLevel)
                return InvalidSavedViewArgument($"'{invalidLevel}' is not a valid log level. Use one of: {String.Join(", ", s_savedViewLevels)}.");

            SavedViewFilterDefinitions.Set(definitions, "level", ToJsonArray(normalizedLevels));
        }

        if (tags is { Length: > 0 })
        {
            string[] normalizedTags = tags.Select(tag => tag.Trim()).ToArray();
            if (normalizedTags.FirstOrDefault(tag => !IsPlainSavedViewValue(tag)) is { } invalidTag)
                return InvalidSavedViewArgument($"'{invalidTag}' is not a valid tag.");

            SavedViewFilterDefinitions.Set(definitions, "tag", ToJsonArray(normalizedTags));
        }

        if (!String.IsNullOrWhiteSpace(query))
        {
            // An unknown field parses and runs, but matches nothing, so the view would always be empty.
            var fieldError = GetFilterFieldError(query, SavedViewFilterFields, allowIndexedDataFields: true);
            if (fieldError is not null)
                return fieldError;

            SavedViewFilterDefinitions.Set(definitions, "keyword", JsonValue.Create(query.Trim()));
        }

        return null;
    }

    private static bool ClearsQuery(string[]? clearCriteria)
        => clearCriteria?.Any(criteria => String.Equals(criteria.Trim(), "query", StringComparison.OrdinalIgnoreCase)) is true;

    private static bool IsExactSavedViewMatch(ViewSavedView view, string name)
        => String.Equals(view.Id, name, StringComparison.Ordinal)
            || String.Equals(view.Name, name, StringComparison.OrdinalIgnoreCase)
            || String.Equals(view.Slug, name, StringComparison.OrdinalIgnoreCase);

    private static bool ChangesSeparateCriteria(string[]? projectIds, bool allProjects, string[]? types, string[]? statuses, string[]? environments, string[]? levels, string[]? tags, string[]? clearCriteria)
        => allProjects
            || new[] { projectIds, types, statuses, environments, levels, tags }.Any(values => values is { Length: > 0 })
            || clearCriteria?.Any(criteria => !String.Equals(criteria.Trim(), "query", StringComparison.OrdinalIgnoreCase)) is true;

    private McpErrorInfo? ValidateSavedViewDisplay(string viewType, string[]? types, string? time, string? sort)
    {
        if (viewType == "sessions" && types is { Length: > 0 })
            return InvalidSavedViewArgument("Sessions views always show sessions, so they cannot filter by event type.");

        if (time is not null)
        {
            if (viewType == "stream")
                return McpErrors.InvalidTimeRange("Stream views show live events, so they have no time range.", time, timeZone: null);

            if (!TryResolveTimeRange(time, timeZone: null, out _, out var timeError))
                return timeError;
        }

        if (sort is null)
            return null;

        if (viewType is not ("events" or "sessions"))
            return McpErrors.InvalidSort("Only events and sessions views can be sorted.", sort, s_savedViewSorts);

        return s_savedViewSorts.Contains(sort) ? null : McpErrors.InvalidSort($"Sort must be one of: {String.Join(", ", s_savedViewSorts)}.", sort, s_savedViewSorts);
    }

    private async Task<McpErrorInfo?> ValidateSavedViewProjectsAsync(string organizationId, string[] projectIds)
    {
        foreach (string projectId in projectIds)
        {
            if (!TryValidateId(projectId, "projectIds", out var idError))
                return idError;
        }

        var projects = await _projectRepository.GetByIdsAsync(projectIds.Distinct(StringComparer.Ordinal).ToArray(), o => o.Cache());
        string? missingProjectId = projectIds.FirstOrDefault(projectId => !projects.Any(project => project.Id == projectId && project.OrganizationId == organizationId));
        return missingProjectId is null ? null : McpErrors.NotFound($"Project {missingProjectId} was not found in this organization.", "projectIds", missingProjectId);
    }

    private static void SetSavedViewProjects(JsonArray definitions, IEnumerable<string> projectIds)
        => SavedViewFilterDefinitions.Set(definitions, "project", ToJsonArray(projectIds.Distinct(StringComparer.Ordinal)));

    private static JsonArray ToJsonArray(IEnumerable<string> values)
        => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    /// <summary>The app quotes tags without escaping, so a quote or control character would break the filter.</summary>
    private static bool IsPlainSavedViewValue(string value)
        => value.Length > 0 && !value.Any(character => character is '"' or '\\' || Char.IsControl(character));

    /// <summary>Strict tool calling fills optional arguments with empty values, which mean the argument was not supplied.</summary>
    private static string? NullIfBlank(string? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static McpSavedViewResult ToSavedViewResult(ViewSavedView view, bool? changed = null, string? message = null) => new(
        view.Id,
        view.OrganizationId,
        view.Name,
        view.Slug,
        view.ViewType,
        view.UserId is not null,
        view.PredefinedKey is not null,
        $"/api/v2/saved-views/{view.Id}",
        String.IsNullOrEmpty(view.Filter) ? null : view.Filter,
        view.Time,
        view.Sort,
        changed,
        message);

    private static McpErrorInfo ToSavedViewError(Foundatio.Mediator.IResult result, string fallbackMessage)
    {
        string message = result.ValidationErrors?.FirstOrDefault()?.ErrorMessage ?? (String.IsNullOrWhiteSpace(result.Message) ? fallbackMessage : result.Message);
        return result.Status switch
        {
            ResultStatus.Conflict => McpErrors.Conflict(message),
            ResultStatus.NotFound => McpErrors.NotFound(message),
            ResultStatus.Forbidden or ResultStatus.Unauthorized => McpErrors.NotAccessible(message),
            ResultStatus.Invalid or ResultStatus.BadRequest => InvalidSavedViewArgument(message),
            _ => McpErrors.QueryFailed(message)
        };
    }

    private static McpErrorInfo InvalidSavedViewArgument(string message) => new(McpErrorCodes.InvalidArgument, message);
}
