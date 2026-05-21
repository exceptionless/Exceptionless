using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
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

    public SavedViewController(
        ISavedViewRepository repository,
        ApiMapper mapper,
        IAppQueryValidator validator,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(repository, mapper, validator, timeProvider, loggerFactory)
    { }

    protected override SavedView MapToModel(NewSavedView newModel)
    {
        var model = _mapper.MapToSavedView(newModel);
        model.Slug = ToSlug(String.IsNullOrWhiteSpace(model.Slug) ? model.Name : model.Slug);
        return model;
    }

    protected override ViewSavedView MapToViewModel(SavedView model)
    {
        var viewModel = _mapper.MapToViewSavedView(model);
        if (String.IsNullOrWhiteSpace(viewModel.Slug))
            viewModel.Slug = ToFallbackSlug(viewModel.Name, viewModel.Id);

        return viewModel;
    }

    protected override List<ViewSavedView> MapToViewModels(IEnumerable<SavedView> models) => models.Select(MapToViewModel).ToList();

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
        AppDiagnostics.SavedViewsSize.Add((int)results.Total);

        var viewModels = MapToViewModels(results.Documents);
        return OkWithResourceLinks(viewModels, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    /// <summary>
    /// Get by organization and view
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="viewType">The dashboard view type (events, issues, stream).</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="404">The organization could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/saved-views/{viewType}")]
    public async Task<ActionResult<IReadOnlyCollection<ViewSavedView>>> GetByViewAsync(string organizationId, string viewType, int page = 1, int limit = 25)
    {
        if (!CanAccessOrganization(organizationId))
            return NotFound();

        if (!NewSavedView.ValidViewTypes.Contains(viewType))
            return NotFound();

        // Reads remain available even when the feature is disabled to preserve access to existing saved views.

        page = GetPage(page);
        limit = GetLimit(limit);
        var results = await _repository.GetByViewForUserAsync(organizationId, viewType, CurrentUser.Id, o => o.PageNumber(page).PageLimit(limit));
        AppDiagnostics.SavedViewsViewTypeSize.Add((int)results.Total);

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
    /// <response code="400">An error occurred while creating the saved view.</response>
    /// <response code="409">The saved view already exists.</response>
    [HttpPost("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/saved-views")]
    [Consumes("application/json")]
    [ProducesResponseType<ViewSavedView>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ViewSavedView>> PostAsync(string organizationId, NewSavedView savedView)
    {
        if (!IsInOrganization(organizationId))
            return BadRequest();

        savedView.OrganizationId = organizationId;
        if (savedView.IsPrivate is true)
            savedView.UserId = CurrentUser.Id;

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

        var count = await _repository.CountByOrganizationIdAsync(value.OrganizationId);
        if (count >= MaxViewsPerOrganization)
            return PermissionResult.DenyWithMessage($"Organization is limited to {MaxViewsPerOrganization} saved views.");

        if (String.IsNullOrWhiteSpace(value.Slug))
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "URL name cannot be empty. Use at least one letter or number.");

        if (IsReservedSlug(value.Slug))
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "URL name cannot look like an event or issue id.");

        if (!IsValidSlug(value.Slug))
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "URL name can only contain lowercase letters, numbers, and single dashes.");

        if (await NameExistsAsync(value.OrganizationId, value.ViewType, value.Name, null))
            return PermissionResult.DenyWithStatus(StatusCodes.Status409Conflict, $"A saved view named '{value.Name.Trim()}' already exists.");

        if (await SlugExistsAsync(value.OrganizationId, value.ViewType, value.Slug, null))
            return PermissionResult.DenyWithStatus(StatusCodes.Status409Conflict, $"A saved view with URL name '{value.Slug}' already exists.");

        return await base.CanAddAsync(value);
    }

    protected override async Task<PermissionResult> CanUpdateAsync(SavedView original, Delta<UpdateSavedView> changes)
    {
        if (original.UserId is not null && original.UserId != CurrentUser.Id && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return PermissionResult.DenyWithNotFound(original.Id);

        // Delta<T> bypasses IValidatableObject — enforce data-annotation and custom validation manually.
        var changedNames = changes.GetChangedPropertyNames();

        if (changedNames.Contains(nameof(UpdateSavedView.Name))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.Name), out object? nameValue)
            && nameValue is string name && String.IsNullOrWhiteSpace(name))
        {
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "Name cannot be empty or whitespace.");
        }

        if (changedNames.Contains(nameof(UpdateSavedView.Slug))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.Slug), out object? slugValue)
            && (slugValue is not string slug || String.IsNullOrWhiteSpace(slug)))
        {
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "URL name cannot be empty. Use at least one letter or number.");
        }

        var lengthResult = ValidateStringLength<UpdateSavedView>(changes, changedNames, nameof(UpdateSavedView.Name), 100)
            ?? ValidateStringLength<UpdateSavedView>(changes, changedNames, nameof(UpdateSavedView.Slug), 100)
            ?? ValidateStringLength<UpdateSavedView>(changes, changedNames, nameof(UpdateSavedView.Filter), 2000)
            ?? ValidateStringLength<UpdateSavedView>(changes, changedNames, nameof(UpdateSavedView.Time), 100)
            ?? ValidateStringLength<UpdateSavedView>(changes, changedNames, nameof(UpdateSavedView.Sort), 100)
            ?? ValidateStringLength<UpdateSavedView>(changes, changedNames, nameof(UpdateSavedView.FilterDefinitions), SavedView.MaxFilterDefinitionsLength);
        if (lengthResult is not null)
            return lengthResult;

        if (changedNames.Contains(nameof(UpdateSavedView.Name))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.Name), out nameValue)
            && nameValue is string changedName
            && await NameExistsAsync(original.OrganizationId, original.ViewType, changedName, original.Id))
        {
            return PermissionResult.DenyWithStatus(StatusCodes.Status409Conflict, $"A saved view named '{changedName.Trim()}' already exists.");
        }

        if (changedNames.Contains(nameof(UpdateSavedView.Slug))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.Slug), out slugValue)
            && slugValue is string changedSlug)
        {
            var normalizedSlug = ToSlug(changedSlug);
            if (IsReservedSlug(normalizedSlug))
                return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "URL name cannot look like an event or issue id.");

            if (!IsValidSlug(normalizedSlug))
                return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "URL name can only contain lowercase letters, numbers, and single dashes.");

            if (await SlugExistsAsync(original.OrganizationId, original.ViewType, normalizedSlug, original.Id))
                return PermissionResult.DenyWithStatus(StatusCodes.Status409Conflict, $"A saved view with URL name '{normalizedSlug}' already exists.");
        }

        if (changedNames.Contains(nameof(UpdateSavedView.FilterDefinitions))
            && changes.TryGetPropertyValue(nameof(UpdateSavedView.FilterDefinitions), out object? filterDefsValue)
            && filterDefsValue is string filterDefs
            && !NewSavedView.IsValidJsonArray(filterDefs))
        {
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, "FilterDefinitions must be a valid JSON array.");
        }

        if (changedNames.Contains(nameof(UpdateSavedView.Columns)) || changedNames.Contains(nameof(UpdateSavedView.ColumnOrder)))
        {
            var patchedChanges = new UpdateSavedView();
            changes.Patch(patchedChanges);

            var validationError = ValidateColumns(original.ViewType, patchedChanges);
            if (validationError is not null)
            {
                return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, validationError.ErrorMessage ?? "Invalid column keys.");
            }
        }

        return await base.CanUpdateAsync(original, changes);
    }

    private static PermissionResult? ValidateStringLength<T>(Delta<T> changes, IEnumerable<string> changedNames, string propertyName, int maxLength) where T : class, new()
    {
        if (changedNames.Contains(propertyName)
            && changes.TryGetPropertyValue(propertyName, out object? value)
            && value is string s && s.Length > maxLength)
        {
            return PermissionResult.DenyWithStatus(StatusCodes.Status422UnprocessableEntity, $"{propertyName} cannot exceed {maxLength} characters.");
        }

        return null;
    }

    private static ValidationResult? ValidateColumns(string viewType, UpdateSavedView changes)
    {
        if (changes.Columns is not null && changes.Columns.Count > 50)
            return new ValidationResult("Columns cannot exceed 50 items.", [nameof(UpdateSavedView.Columns)]);

        if (changes.ColumnOrder is not null && changes.ColumnOrder.Count > 50)
            return new ValidationResult("ColumnOrder cannot exceed 50 items.", [nameof(UpdateSavedView.ColumnOrder)]);

        return NewSavedView.ValidateColumnKeys(viewType, changes.Columns)
            .Concat(NewSavedView.ValidateColumnOrder(viewType, changes.ColumnOrder))
            .FirstOrDefault();
    }

    protected override Task<SavedView> AddModelAsync(SavedView value)
    {
        value.CreatedByUserId = CurrentUser.Id;
        value.Version = 1;

        return base.AddModelAsync(value);
    }

    protected override Task<SavedView> UpdateModelAsync(SavedView original, Delta<UpdateSavedView> changes)
    {
        var changedNames = changes.GetChangedPropertyNames();
        changes.Patch(original);

        if (changedNames.Contains(nameof(UpdateSavedView.Slug)))
            original.Slug = ToSlug(original.Slug);

        if (String.IsNullOrWhiteSpace(original.Slug))
            original.Slug = ToFallbackSlug(original.Name, original.Id);

        original.UpdatedByUserId = CurrentUser.Id;

        return _repository.SaveAsync(original, o => o.Cache());
    }

    protected override async Task<PermissionResult> CanDeleteAsync(SavedView value)
    {
        if (value.UserId is not null && value.UserId != CurrentUser.Id && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return PermissionResult.DenyWithNotFound(value.Id);

        return await base.CanDeleteAsync(value);
    }

    private async Task<bool> SlugExistsAsync(string organizationId, string viewType, string slug, string? excludingId)
    {
        var results = await _repository.GetByViewForUserAsync(organizationId, viewType, CurrentUser.Id, o => o.PageLimit(1000));
        return results.Documents.Any(view => view.Id != excludingId && String.Equals(ToFallbackSlug(String.IsNullOrWhiteSpace(view.Slug) ? view.Name : view.Slug, view.Id), slug, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> NameExistsAsync(string organizationId, string viewType, string name, string? excludingId)
    {
        var results = await _repository.GetByViewForUserAsync(organizationId, viewType, CurrentUser.Id, o => o.PageLimit(1000));
        return results.Documents.Any(view => view.Id != excludingId && String.Equals(view.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidSlug(string slug)
    {
        return Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$") && !IsReservedSlug(slug);
    }

    private static bool IsReservedSlug(string slug)
    {
        return Regex.IsMatch(slug, "^[a-f0-9]{24}$");
    }

    private static string ToSlug(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        slug = Regex.Replace(slug, "-+", "-");
        return slug;
    }

    private static string ToFallbackSlug(string value, string id)
    {
        var slug = ToSlug(value);
        if (!String.IsNullOrWhiteSpace(slug))
            return slug;

        return String.IsNullOrWhiteSpace(id) ? "saved-view" : $"saved-view-{id}";
    }
}
