using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.App.Controllers.API;

[Route(API_PREFIX + "/saved-views")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class SavedViewController : RepositoryApiController<ISavedViewRepository, SavedView, ViewSavedView, NewSavedView, UpdateSavedView>
{
    private const int MaxViewsPerOrganization = 100;
    private bool _isPrivateRequest;
    private readonly IOrganizationRepository _organizationRepository;

    public SavedViewController(
        ISavedViewRepository repository,
        IOrganizationRepository organizationRepository,
        ApiMapper mapper,
        IAppQueryValidator validator,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(repository, mapper, validator, timeProvider, loggerFactory)
    {
        _organizationRepository = organizationRepository;
    }

    protected override SavedView MapToModel(NewSavedView newModel) => _mapper.MapToSavedView(newModel);
    protected override ViewSavedView MapToViewModel(SavedView model) => _mapper.MapToViewSavedView(model);
    protected override List<ViewSavedView> MapToViewModels(IEnumerable<SavedView> models) => _mapper.MapToViewSavedViews(models);

    private async Task<bool> IsFeatureEnabledAsync(string organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
        return organization?.Features?.Contains(OrganizationFeatures.SavedViews) ?? false;
    }

    /// <summary>
    /// Get by organization
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="404">The organization could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/saved-views")]
    public async Task<ActionResult<IReadOnlyCollection<ViewSavedView>>> GetByOrganizationAsync(string organizationId, int page = 1, int limit = 25)
    {
        if (!CanAccessOrganization(organizationId))
            return NotFound();

        // Reads remain available even when the feature is disabled to preserve access to existing saved views.

        page = GetPage(page);
        limit = GetLimit(limit);
        var results = await _repository.GetByOrganizationForUserAsync(organizationId, CurrentUser.Id, o => o.PageNumber(page).PageLimit(limit));

        var viewModels = MapToViewModels(results.Documents);
        return OkWithResourceLinks(viewModels, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    /// <summary>
    /// Get by organization and view
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="view">The dashboard view (events, issues, stream).</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="404">The organization could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/saved-views/{view}")]
    public async Task<ActionResult<IReadOnlyCollection<ViewSavedView>>> GetByViewAsync(string organizationId, string view, int page = 1, int limit = 25)
    {
        if (!CanAccessOrganization(organizationId))
            return NotFound();

        if (!SavedView.ValidViews.Contains(view))
            return NotFound();

        // Reads remain available even when the feature is disabled to preserve access to existing saved views.

        page = GetPage(page);
        limit = GetLimit(limit);
        var results = await _repository.GetByViewForUserAsync(organizationId, view, CurrentUser.Id, o => o.PageNumber(page).PageLimit(limit));

        var viewModels = MapToViewModels(results.Documents);
        return OkWithResourceLinks(viewModels, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    /// <summary>
    /// Get by id
    /// </summary>
    /// <param name="id">The identifier of the saved view.</param>
    /// <response code="404">The saved view could not be found.</response>
    [HttpGet("{id:objectid}", Name = "GetSavedViewById")]
    public Task<ActionResult<ViewSavedView>> GetAsync(string id)
    {
        return GetByIdImplAsync(id);
    }

    /// <summary>
    /// Create
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="savedView">The saved view.</param>
    /// <param name="is_private">If true, the view will only be visible to the current user.</param>
    /// <response code="400">An error occurred while creating the saved view.</response>
    /// <response code="409">The saved view already exists.</response>
    [HttpPost("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/saved-views")]
    [Consumes("application/json")]
    [ProducesResponseType<ViewSavedView>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ViewSavedView>> PostAsync(string organizationId, NewSavedView savedView, [FromQuery] bool is_private = false)
    {
        if (!IsInOrganization(organizationId))
            return BadRequest();

        if (is_private && savedView.IsDefault)
        {
            ModelState.AddModelError(nameof(NewSavedView.IsDefault), "Private views cannot be set as the default. Default views are organization-wide.");
            return ValidationProblem(ModelState);
        }

        savedView.OrganizationId = organizationId;
        _isPrivateRequest = is_private;
        return await PostImplAsync(savedView);
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="id">The identifier of the saved view.</param>
    /// <param name="changes">The changes</param>
    /// <response code="400">An error occurred while updating the saved view.</response>
    /// <response code="404">The saved view could not be found.</response>
    [HttpPatch("{id:objectid}")]
    [HttpPut("{id:objectid}")]
    [Consumes("application/json")]
    public Task<ActionResult<ViewSavedView>> PatchAsync(string id, Delta<UpdateSavedView> changes)
    {
        return PatchImplAsync(id, changes);
    }

    /// <summary>
    /// Remove
    /// </summary>
    /// <param name="ids">A comma-delimited list of saved view identifiers.</param>
    /// <response code="202">Accepted</response>
    /// <response code="400">One or more validation errors occurred.</response>
    /// <response code="404">One or more saved views were not found.</response>
    /// <response code="500">An error occurred while deleting one or more saved views.</response>
    [HttpDelete("{ids:objectids}")]
    [ProducesResponseType<WorkInProgressResult>(StatusCodes.Status202Accepted)]
    public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids)
    {
        return DeleteImplAsync(ids.FromDelimitedString());
    }

    protected override async Task<SavedView?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await _repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!String.IsNullOrEmpty(model.OrganizationId) && !IsInOrganization(model.OrganizationId))
            return null;

        if (model.UserId is not null && model.UserId != CurrentUser.Id && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return null;

        return model;
    }

    protected override async Task<PermissionResult> CanAddAsync(SavedView value)
    {
        if (String.IsNullOrEmpty(value.OrganizationId) || !IsInOrganization(value.OrganizationId))
            return PermissionResult.Deny;

        if (!await IsFeatureEnabledAsync(value.OrganizationId))
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "The saved views feature is not enabled for this organization.");

        var count = await _repository.CountByOrganizationIdAsync(value.OrganizationId);
        if (count >= MaxViewsPerOrganization)
            return PermissionResult.DenyWithMessage($"Organization is limited to {MaxViewsPerOrganization} saved views.");

        return await base.CanAddAsync(value);
    }

    protected override async Task<PermissionResult> CanUpdateAsync(SavedView original, Delta<UpdateSavedView> changes)
    {
        if (original.UserId is not null && original.UserId != CurrentUser.Id && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return PermissionResult.DenyWithNotFound(original.Id);

        if (!await IsFeatureEnabledAsync(original.OrganizationId))
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "The saved views feature is not enabled for this organization.");

        // Private views cannot be set as the default
        if (original.UserId is not null
            && changes.GetChangedPropertyNames().Contains(nameof(UpdateSavedView.IsDefault))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.IsDefault), out object? isDefaultValue)
            && isDefaultValue is true)
        {
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "Private views cannot be set as the default. Default views are organization-wide.");
        }

        if (changes.GetChangedPropertyNames().Contains(nameof(UpdateSavedView.Columns)))
        {
            var patchedChanges = new UpdateSavedView();
            changes.Patch(patchedChanges);
            var validationError = NewSavedView.ValidateColumnKeys(original.View, patchedChanges.Columns).FirstOrDefault();
            if (validationError is not null)
            {
                return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, validationError.ErrorMessage ?? "Invalid column keys.");
            }
        }

        return await base.CanUpdateAsync(original, changes);
    }

    protected override async Task<SavedView> AddModelAsync(SavedView value)
    {
        value.CreatedUtc = value.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        value.CreatedByUserId = CurrentUser.Id;
        value.Version = 1;

        if (_isPrivateRequest)
            value.UserId = CurrentUser.Id;

        if (value.IsDefault)
            await ClearDefaultForViewAsync(value.OrganizationId, value.View);

        return await base.AddModelAsync(value);
    }

    protected override async Task<SavedView> UpdateModelAsync(SavedView original, Delta<UpdateSavedView> changes)
    {
        original.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        original.UpdatedByUserId = CurrentUser.Id;

        if (changes.GetChangedPropertyNames().Contains(nameof(UpdateSavedView.IsDefault))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.IsDefault), out object? isDefaultValue)
            && isDefaultValue is true)
        {
            await ClearDefaultForViewAsync(original.OrganizationId, original.View);
        }

        return await base.UpdateModelAsync(original, changes);
    }

    private async Task ClearDefaultForViewAsync(string organizationId, string view)
    {
        var existing = await _repository.GetByViewAsync(organizationId, view, o => o.ImmediateConsistency());
        var defaults = existing.Documents.Where(savedView => savedView.IsDefault && savedView.UserId is null).ToList();

        if (defaults.Count > 0)
        {
            foreach (var defaultView in defaults)
            {
                defaultView.IsDefault = false;
            }

            await _repository.SaveAsync(defaults, o => o.ImmediateConsistency());
        }
    }

    protected override async Task<PermissionResult> CanDeleteAsync(SavedView value)
    {
        if (value.UserId is not null && value.UserId != CurrentUser.Id && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return PermissionResult.DenyWithNotFound(value.Id);

        if (!await IsFeatureEnabledAsync(value.OrganizationId))
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "The saved views feature is not enabled for this organization.");

        return await base.CanDeleteAsync(value);
    }
}
