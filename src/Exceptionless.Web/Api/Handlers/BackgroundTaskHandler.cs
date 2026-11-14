using System.Text.RegularExpressions;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.DateTimeExtensions;
using Exceptionless.DateTimeExtensions.FormatParsers;
using Exceptionless.DateTimeExtensions.FormatParsers.PartParsers;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Web.Api.Handlers;

public class BackgroundTaskHandler(
    BackgroundTaskService backgroundTaskService,
    BulkOperationService bulkOperationService,
    IEnumerable<IBulkSelectionResolver> resolvers,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    AppOptions appOptions,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<BackgroundTaskHandler> logger)
{
    private const int MaxIds = 1000;

    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<AcceptedResponse<ViewBackgroundTask>>> Handle(CreateBulkOperation message)
    {
        var request = HttpContext.Request;
        var model = message.Operation;

        var handler = bulkOperationService.GetHandler(model.Type);
        if (handler is null)
            return Result.Invalid(ValidationError.Create("type", $"Unknown bulk operation type '{model.Type}'."));

        // Organization and project access mirrors the single-item actions: not accessible looks the same as not found.
        if (!request.CanAccessOrganization(model.OrganizationId))
            return Result.NotFound("Organization not found.");

        var organization = await organizationRepository.GetByIdAsync(model.OrganizationId, o => o.Cache());
        if (organization is null)
            return Result.NotFound("Organization not found.");

        Project? project = null;
        if (!String.IsNullOrEmpty(model.ProjectId))
        {
            project = await projectRepository.GetByIdAsync(model.ProjectId, o => o.Cache());
            if (project is null || !String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal))
                return Result.NotFound("Project not found.");
        }

        var user = request.GetUser();
        var actor = new BulkOperationActor(user.Id, request.IsGlobalAdmin(), request.GetAssociatedOrganizationIds().ToList());
        var parameters = model.Parameters ?? new Dictionary<string, string>();

        var selectionResult = ResolveSelection(model.Selection, organization);
        if (selectionResult.Result is not null)
            return selectionResult.Result;

        var selection = selectionResult.Selection!;
        var bulkRequest = new BulkOperationRequest(actor, organization, project, handler.Type, selection, parameters);

        var authorized = await handler.AuthorizeAsync(bulkRequest);
        if (!authorized.IsOk)
            return ToResult<AcceptedResponse<ViewBackgroundTask>>(authorized);

        var validated = await handler.ValidateAsync(bulkRequest);
        if (!validated.IsOk)
            return ToResult<AcceptedResponse<ViewBackgroundTask>>(validated);

        var task = new BackgroundTask
        {
            OrganizationId = organization.Id,
            ProjectId = project?.Id,
            Type = handler.Type,
            CreatedByUserId = user.Id,
            Bulk = new BulkOperationDetails
            {
                Selection = selection,
                Parameters = new Dictionary<string, string>(parameters)
            }
        };

        if (selection.Kind is BulkOperationSelectionKind.Filter)
        {
            var resolver = resolvers.FirstOrDefault(r => r.Entity == handler.Entity);
            if (resolver is null)
                return Result.Error($"No selection resolver is registered for {handler.Entity}.");

            var selectionCheck = await resolver.ValidateSelectionAsync(organization, selection);
            if (!selectionCheck.IsOk)
                return ToResult<AcceptedResponse<ViewBackgroundTask>>(selectionCheck);
        }

        // Counting and pinning the selection (and describing the operation) happens while it is created.
        var created = await bulkOperationService.CreateAsync(task);
        if (created.Rejection is not null)
            return ToResult<AcceptedResponse<ViewBackgroundTask>>(created.Rejection);

        if (created.Conflict is not null)
            return Result.Conflict("Another destructive operation is already running for this project. Wait for it to finish or cancel it first.");

        if (created.Task is null)
            return Result.Conflict("Another bulk operation is being started for this organization. Please try again.");

        using var _ = logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Tag("BulkOperation").Identity(user.EmailAddress));
        logger.LogInformation("User {User} started bulk operation {TaskId} {Type} for {Total} items", user.Id, created.Task.Id, created.Task.Type, created.Task.Total);

        return new AcceptedResponse<ViewBackgroundTask>(MapToViewModel(created.Task));
    }

    public async Task<Result<ViewBackgroundTask>> Handle(GetBackgroundTaskById message)
    {
        var task = await GetAccessibleAsync(message.Id);
        if (task is null)
            return Result.NotFound("Task not found.");

        return MapToViewModel(task);
    }

    public async Task<Result<IReadOnlyCollection<ViewBackgroundTask>>> Handle(GetBackgroundTasksByOrganization message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        var tasks = await backgroundTaskService.GetByOrganizationAsync(message.OrganizationId);
        return Result<IReadOnlyCollection<ViewBackgroundTask>>.Success(tasks.Select(MapToViewModel).ToList());
    }

    public async Task<Result<AcceptedResponse<ViewBackgroundTask>>> Handle(CancelBackgroundTask message)
    {
        var task = await GetAccessibleAsync(message.Id);
        if (task is null)
            return Result.NotFound("Task not found.");

        var cancelled = await backgroundTaskService.CancelAsync(task.Id);
        if (cancelled.Task is null)
            return Result.NotFound("Task not found.");

        if (cancelled.IsNotCancellable)
            return Result.Invalid(ValidationError.Create("task", "This task cannot be cancelled."));

        if (!cancelled.Task.IsActive)
            await bulkOperationService.ReleaseSelectionAsync(cancelled.Task);

        logger.LogInformation("User {User} requested cancellation of task {TaskId}", HttpContext.Request.GetUser().Id, cancelled.Task.Id);
        return new AcceptedResponse<ViewBackgroundTask>(MapToViewModel(cancelled.Task));
    }

    private async Task<BackgroundTask?> GetAccessibleAsync(string id)
    {
        var task = await backgroundTaskService.GetAsync(id);
        if (task is null || !HttpContext.Request.CanAccessOrganization(task.OrganizationId))
            return null;

        return task;
    }

    private (BulkOperationSelection? Selection, Result? Result) ResolveSelection(NewBulkOperationSelection model, Organization organization)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        if (model.Kind is BulkOperationSelectionKind.Ids)
        {
            if (model.Filter is not null || model.Time is not null)
                return (null, Result.Invalid(ValidationError.Create("selection", "An id selection cannot include a filter or time.")));

            var ids = (model.Ids ?? []).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count == 0)
                return (null, Result.Invalid(ValidationError.Create("selection.ids", "At least one id is required.")));

            if (ids.Count > MaxIds)
                return (null, Result.Invalid(ValidationError.Create("selection.ids", $"At most {MaxIds} ids can be selected. Use a filter selection for more.")));

            if (ids.Any(id => !ObjectId.TryParse(id, out _)))
                return (null, Result.Invalid(ValidationError.Create("selection.ids", "Every id must be a valid identifier.")));

            return (new BulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = ids }, null);
        }

        // Only an explicit filter selection may resolve a filter. An unknown kind must never fall through to an empty filter,
        // which would select everything in scope.
        if (model.Kind is not BulkOperationSelectionKind.Filter)
            return (null, Result.Invalid(ValidationError.Create("selection.kind", "The selection kind must be ids or filter.")));

        if (model.Ids is { Count: > 0 })
            return (null, Result.Invalid(ValidationError.Create("selection", "A filter selection cannot include ids.")));

        if (!TryParseSelectionOffset(model.Offset, out var utcOffset))
            return (null, Result.Invalid(ValidationError.Create("selection.offset", "The UTC offset must be a valid whole-minute offset between -14h and 14h.")));

        // Validate the requested range before retention and open-end defaults can turn a parse failure into all history.
        // Reuse the parsed range so validation and execution resolve relative dates at exactly the same instant.
        if (!TryParseSelectionTime(model.Time, new DateTimeOffset(utcNow).ToOffset(utcOffset), out var range))
            return (null, Result.Invalid(ValidationError.Create("selection.time", "The time range is not valid.")));

        var timeInfo = new TimeInfo { Field = EventIndex.Alias.Date, Offset = utcOffset, Range = range! };
        timeInfo.ApplyMinimumUtcStartDate(organization.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        timeInfo.AdjustEndTimeIfMaxValue(timeProvider);
        var utcStart = timeInfo.Range.UtcStart;
        var utcEnd = timeInfo.Range.UtcEnd > utcNow ? utcNow : timeInfo.Range.UtcEnd;
        if (utcStart > utcEnd)
            return (null, Result.Invalid(ValidationError.Create("selection.time", "The time range does not include any past time.")));

        var selection = new BulkOperationSelection
        {
            Kind = BulkOperationSelectionKind.Filter,
            Filter = model.Filter?.Trim() ?? String.Empty,
            Time = model.Time,
            UtcStart = utcStart,
            UtcEnd = utcEnd,
            UtcCreatedBefore = utcNow
        };

        return (selection, null);
    }

    internal static bool TryParseSelectionTime(string? time, DateTimeOffset relativeBaseTime, out DateTimeRange? range)
    {
        if (String.IsNullOrWhiteSpace(time))
        {
            range = DateTimeRange.Empty;
            return true;
        }

        string expression = time.Trim();
        if (expression.StartsWith($"{EventIndex.Alias.Date}|", StringComparison.Ordinal))
            expression = expression[(EventIndex.Alias.Date.Length + 1)..];

        // DateTimeRange.Parse uses the same unbounded sentinel for invalid input and a valid [* TO *] range.
        // Its two-part parser also supplies missing bounds, so require both to have been parsed explicitly.
        range = null;
        try
        {
            foreach (var parser in DateTimeRange.FormatParsers)
            {
                if (parser is TwoPartFormatParser twoPartParser)
                {
                    var bounds = new List<DateTimeOffset>(2);
                    var partParsers = twoPartParser.Parsers.Select<IPartParser, IPartParser>(partParser => partParser is WildcardPartParser
                        ? new SelectionTimeWildcardPartParser(bounds)
                        : new SelectionTimePartParser(partParser, bounds));
                    range = new TwoPartFormatParser(partParsers).Parse(expression, relativeBaseTime);
                    if (range is not null && (bounds.Count != 2 || bounds[0] > bounds[1]))
                    {
                        range = null;
                        return false;
                    }
                }
                else
                {
                    range = parser.Parse(expression, relativeBaseTime);
                }

                if (range is not null)
                    return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            range = null;
        }

        return false;
    }

    internal static bool TryParseSelectionOffset(string? offset, out TimeSpan utcOffset)
    {
        utcOffset = TimeSpan.Zero;
        if (String.IsNullOrWhiteSpace(offset))
            return true;

        try
        {
            if (!TimeUnit.TryParse(offset, out var parsedOffset) || !parsedOffset.HasValue
                || parsedOffset.Value < TimeSpan.FromHours(-14) || parsedOffset.Value > TimeSpan.FromHours(14)
                || parsedOffset.Value.Ticks % TimeSpan.TicksPerMinute != 0)
                return false;

            utcOffset = parsedOffset.Value;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private sealed class SelectionTimePartParser(IPartParser inner, List<DateTimeOffset> bounds) : IPartParser
    {
        public Regex Regex => inner.Regex;

        public DateTimeOffset? Parse(Match match, DateTimeOffset relativeBaseTime, bool isUpperLimit)
        {
            var value = inner.Parse(match, relativeBaseTime, isUpperLimit);
            if (value.HasValue)
                bounds.Add(value.Value);

            return value;
        }
    }

    // The two-part parser checks this type to keep wildcard bounds open regardless of bracket inclusivity.
    private sealed class SelectionTimeWildcardPartParser(List<DateTimeOffset> bounds) : WildcardPartParser, IPartParser
    {
        DateTimeOffset? IPartParser.Parse(Match match, DateTimeOffset relativeBaseTime, bool isUpperLimit)
        {
            var value = base.Parse(match, relativeBaseTime, isUpperLimit);
            if (value.HasValue)
                bounds.Add(value.Value);

            return value;
        }
    }

    private static Result<T> ToResult<T>(BulkOperationCheckResult check)
    {
        return check.Status switch
        {
            BulkOperationCheckStatus.Denied => Result.Forbidden(check.Message ?? "Forbidden"),
            BulkOperationCheckStatus.PlanLimit => Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, check.Message ?? "Please upgrade your plan.")),
            _ => Result.Invalid(ValidationError.Create(check.Field ?? "operation", check.Message ?? "Invalid request."))
        };
    }

    private static ViewBackgroundTask MapToViewModel(BackgroundTask task)
    {
        var selection = task.Bulk?.Selection;
        return new ViewBackgroundTask
        {
            Id = task.Id,
            OrganizationId = task.OrganizationId,
            ProjectId = task.ProjectId,
            Type = task.Type,
            Description = task.Description,
            Status = task.Status,
            Progress = task.Progress,
            Total = task.Total,
            Processed = task.Processed,
            Succeeded = task.Succeeded,
            Failed = task.Failed,
            CanCancel = task.CanCancel,
            Selection = selection is null ? null : new ViewBulkOperationSelection
            {
                Kind = selection.Kind,
                IdCount = selection.Kind is BulkOperationSelectionKind.Ids ? selection.Ids?.Count : null,
                Filter = selection.Filter,
                Time = selection.Time,
                UtcStart = selection.UtcStart,
                UtcEnd = selection.UtcEnd
            },
            CreatedByUserId = task.CreatedByUserId,
            CreatedUtc = task.CreatedUtc,
            StartedUtc = task.StartedUtc,
            CompletedUtc = task.CompletedUtc,
            UpdatedUtc = task.UpdatedUtc,
            Message = task.Message
        };
    }
}
