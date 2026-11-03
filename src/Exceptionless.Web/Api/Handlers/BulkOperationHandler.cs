using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Web.Api.Handlers;

public class BulkOperationHandler(
    BulkOperationService bulkOperationService,
    IEnumerable<IBulkSelectionResolver> resolvers,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    AppOptions appOptions,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<BulkOperationHandler> logger)
{
    private const int MaxIds = 1000;
    private static readonly ICollection<string> _allowedDateFields = new List<string> { EventIndex.Alias.Date };

    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<AcceptedResponse<ViewBulkOperation>>> Handle(CreateBulkOperation message)
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
            return ToResult<AcceptedResponse<ViewBulkOperation>>(authorized);

        var validated = await handler.ValidateAsync(bulkRequest);
        if (!validated.IsOk)
            return ToResult<AcceptedResponse<ViewBulkOperation>>(validated);

        var operation = new BulkOperation
        {
            OrganizationId = organization.Id,
            ProjectId = project?.Id,
            Type = handler.Type,
            Selection = selection,
            Parameters = new Dictionary<string, string>(parameters),
            CreatedByUserId = user.Id
        };

        if (selection.Kind is BulkOperationSelectionKind.Filter)
        {
            var resolver = resolvers.FirstOrDefault(r => r.Entity == handler.Entity);
            if (resolver is null)
                return Result.Error($"No selection resolver is registered for {handler.Entity}.");

            var selectionCheck = await resolver.ValidateSelectionAsync(organization, selection);
            if (!selectionCheck.IsOk)
                return ToResult<AcceptedResponse<ViewBulkOperation>>(selectionCheck);
        }

        // Counting and pinning the selection (and describing the operation) happens while it is created.
        var created = await bulkOperationService.CreateAsync(operation);
        if (created.Rejection is not null)
            return ToResult<AcceptedResponse<ViewBulkOperation>>(created.Rejection);

        if (created.Conflict is not null)
            return Result.Conflict("Another destructive operation is already running for this project. Wait for it to finish or cancel it first.");

        if (created.Operation is null)
            return Result.Conflict("Another bulk operation is being started for this organization. Please try again.");

        using var _ = logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Tag("BulkOperation").Identity(user.EmailAddress));
        logger.LogInformation("User {User} started bulk operation {OperationId} {Type} for {Total} items", user.Id, created.Operation.Id, created.Operation.Type, created.Operation.Total);

        return new AcceptedResponse<ViewBulkOperation>(MapToViewModel(created.Operation));
    }

    public async Task<Result<ViewBulkOperation>> Handle(GetBulkOperationById message)
    {
        var operation = await GetAccessibleAsync(message.Id);
        if (operation is null)
            return Result.NotFound("Bulk operation not found.");

        return MapToViewModel(operation);
    }

    public async Task<Result<IReadOnlyCollection<ViewBulkOperation>>> Handle(GetBulkOperationsByOrganization message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        var operations = await bulkOperationService.GetByOrganizationAsync(message.OrganizationId);
        return Result<IReadOnlyCollection<ViewBulkOperation>>.Success(operations.Select(MapToViewModel).ToList());
    }

    public async Task<Result<AcceptedResponse<ViewBulkOperation>>> Handle(CancelBulkOperation message)
    {
        var operation = await GetAccessibleAsync(message.Id);
        if (operation is null)
            return Result.NotFound("Bulk operation not found.");

        var cancelled = await bulkOperationService.CancelAsync(operation.Id);
        if (cancelled is null)
            return Result.NotFound("Bulk operation not found.");

        logger.LogInformation("User {User} requested cancellation of bulk operation {OperationId}", HttpContext.Request.GetUser().Id, cancelled.Id);
        return new AcceptedResponse<ViewBulkOperation>(MapToViewModel(cancelled));
    }

    private async Task<BulkOperation?> GetAccessibleAsync(string id)
    {
        var operation = await bulkOperationService.GetAsync(id);
        if (operation is null || !HttpContext.Request.CanAccessOrganization(operation.OrganizationId))
            return null;

        return operation;
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

        if (model.Ids is { Count: > 0 })
            return (null, Result.Invalid(ValidationError.Create("selection", "A filter selection cannot include ids.")));

        // Resolve the relative time range once, now. The end can never be later than this moment, so events that
        // arrive after the operation is created are not part of it.
        var timeInfo = TimeRangeParser.GetTimeInfo(model.Time, model.Offset, timeProvider, _allowedDateFields, EventIndex.Alias.Date,
            organization.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
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

    private static Result<T> ToResult<T>(BulkOperationCheckResult check)
    {
        return check.Status switch
        {
            BulkOperationCheckStatus.Denied => Result.Forbidden(check.Message ?? "Forbidden"),
            BulkOperationCheckStatus.PlanLimit => Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, check.Message ?? "Please upgrade your plan.")),
            _ => Result.Invalid(ValidationError.Create(check.Field ?? "operation", check.Message ?? "Invalid request."))
        };
    }

    private static ViewBulkOperation MapToViewModel(BulkOperation operation)
    {
        return new ViewBulkOperation
        {
            Id = operation.Id,
            OrganizationId = operation.OrganizationId,
            ProjectId = operation.ProjectId,
            Type = operation.Type,
            Description = operation.Description,
            Status = operation.Status,
            Selection = new ViewBulkOperationSelection
            {
                Kind = operation.Selection.Kind,
                IdCount = operation.Selection.Kind is BulkOperationSelectionKind.Ids ? operation.Selection.Ids?.Count : null,
                Filter = operation.Selection.Filter,
                Time = operation.Selection.Time,
                UtcStart = operation.Selection.UtcStart,
                UtcEnd = operation.Selection.UtcEnd
            },
            Total = operation.Total,
            Processed = operation.Processed,
            Succeeded = operation.Succeeded,
            Failed = operation.Failed,
            CreatedByUserId = operation.CreatedByUserId,
            CreatedUtc = operation.CreatedUtc,
            StartedUtc = operation.StartedUtc,
            CompletedUtc = operation.CompletedUtc,
            UpdatedUtc = operation.UpdatedUtc,
            Message = operation.Message
        };
    }
}
