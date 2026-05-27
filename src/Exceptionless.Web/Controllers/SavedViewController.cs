using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.App.Controllers.API;

[Route(API_PREFIX + "/saved-views")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class SavedViewController : RepositoryApiController<ISavedViewRepository, SavedView, ViewSavedView, NewSavedView, UpdateSavedView>
{
    private const int MaxViewsPerOrganization = 100;
    private const string PredefinedSavedViewsDataKey = "@@PredefinedSavedViewsVersion";
    private const int PredefinedSavedViewsVersion = 3;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILockProvider _lockProvider;

    public SavedViewController(
        ISavedViewRepository repository,
        IOrganizationRepository organizationRepository,
        ILockProvider lockProvider,
        ApiMapper mapper,
        IAppQueryValidator validator,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(repository, mapper, validator, timeProvider, loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _lockProvider = lockProvider;
    }

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
        await EnsurePredefinedSavedViewsCreatedAsync(organizationId);

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
    /// <param name="viewType">The dashboard view type (events, stacks, stream).</param>
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
        await EnsurePredefinedSavedViewsCreatedAsync(organizationId);

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
    /// Create or update predefined saved views
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <response code="200">The predefined saved views were created or updated.</response>
    /// <response code="404">The organization could not be found.</response>
    [HttpPost("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/saved-views/predefined")]
    public async Task<ActionResult<IReadOnlyCollection<ViewSavedView>>> PostPredefinedAsync(string organizationId)
    {
        if (!IsInOrganization(organizationId))
            return NotFound();

        var savedViews = await UpsertPredefinedSavedViewsAsync(organizationId);
        return Ok(MapToViewModels(savedViews));
    }

    /// <summary>
    /// Get global predefined saved views as seed JSON
    /// </summary>
    /// <response code="200">The current predefined saved views.</response>
    [HttpGet("predefined")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult<IReadOnlyCollection<PredefinedSavedViewDefinition>>> GetPredefinedAsync()
    {
        return Ok(await GetPredefinedSavedViewsAsync());
    }

    /// <summary>
    /// Save a saved view as a global predefined saved view
    /// </summary>
    /// <param name="id">The identifier of the saved view to promote.</param>
    /// <response code="200">The predefined saved view was created or updated.</response>
    /// <response code="404">The saved view could not be found.</response>
    [HttpPost("{id:objectid}/predefined")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult<ViewSavedView>> PostPredefinedSavedViewAsync(string id)
    {
        var source = await _repository.GetByIdAsync(id);
        if (source is null)
            return NotFound();

        var savedView = await UpsertSystemPredefinedSavedViewAsync(source);
        return Ok(MapToViewModel(savedView));
    }

    /// <summary>
    /// Delete a global predefined saved view
    /// </summary>
    /// <param name="id">The identifier of the saved view whose predefined saved view should be deleted.</param>
    /// <response code="204">The predefined saved view was deleted.</response>
    /// <response code="404">The saved view could not be found.</response>
    [HttpDelete("{id:objectid}/predefined")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    public async Task<ActionResult> DeletePredefinedSavedViewAsync(string id)
    {
        var source = await _repository.GetByIdAsync(id);
        if (source is null)
            return NotFound();

        await DeleteSystemPredefinedSavedViewAsync(source);
        return NoContent();
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

    private async Task EnsurePredefinedSavedViewsCreatedAsync(string organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is null || HasCreatedPredefinedSavedViews(organization))
            return;

        await UpsertPredefinedSavedViewsAsync(organizationId, true);
    }

    private async Task<IReadOnlyCollection<SavedView>> UpsertPredefinedSavedViewsAsync(string organizationId, bool onlyIfNeverCreated = false)
    {
        List<SavedView> savedViews = [];

        bool lockAcquired = await _lockProvider.TryUsingAsync($"predefined-saved-views:{organizationId}", async () =>
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization is null)
                return;

            if (onlyIfNeverCreated && HasCreatedPredefinedSavedViews(organization))
            {
                savedViews = await GetExistingPredefinedSavedViewsForOrganizationAsync(organizationId);
                return;
            }

            savedViews = await UpsertPredefinedSavedViewsForOrganizationAsync(organizationId);
            organization.Data ??= new DataDictionary();
            organization.Data[PredefinedSavedViewsDataKey] = PredefinedSavedViewsVersion.ToString();
            await _organizationRepository.SaveAsync(organization, o => o.Cache().ImmediateConsistency());
        }, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));

        if (!lockAcquired)
            return await GetExistingPredefinedSavedViewsForOrganizationAsync(organizationId);

        return savedViews;
    }

    private async Task<List<SavedView>> UpsertPredefinedSavedViewsForOrganizationAsync(string organizationId)
    {
        var savedViewsByView = new Dictionary<string, List<SavedView>>(StringComparer.OrdinalIgnoreCase);
        var upserted = new List<SavedView>();

        var definitions = await GetPredefinedSavedViewsAsync();
        foreach (var definition in definitions)
        {
            if (!savedViewsByView.TryGetValue(definition.ViewType, out var existingViews))
            {
                var results = await _repository.GetByViewAsync(organizationId, definition.ViewType, o => o.PageLimit(1000));
                existingViews = results.Documents.ToList();
                savedViewsByView.Add(definition.ViewType, existingViews);
            }

            var existing = FindPredefinedSavedView(definition, existingViews);
            var slug = GetUniqueSlug(definition.Slug, existingViews, existing?.Id);

            if (existing is null)
            {
                var savedView = CreatePredefinedSavedView(organizationId, definition, slug);
                await _repository.AddAsync(savedView, o => o.Cache().ImmediateConsistency());
                existingViews.Add(savedView);
                upserted.Add(savedView);
                continue;
            }

            if (ApplyPredefinedSavedView(existing, definition, slug))
            {
                existing.UpdatedByUserId = CurrentUser.Id;
                await _repository.SaveAsync(existing, o => o.Cache().ImmediateConsistency());
            }

            upserted.Add(existing);
        }

        return upserted;
    }

    private async Task<List<SavedView>> GetExistingPredefinedSavedViewsForOrganizationAsync(string organizationId)
    {
        var savedViewsByView = new Dictionary<string, List<SavedView>>(StringComparer.OrdinalIgnoreCase);
        var existingPredefinedViews = new List<SavedView>();

        var definitions = await GetPredefinedSavedViewsAsync();
        foreach (var definition in definitions)
        {
            if (!savedViewsByView.TryGetValue(definition.ViewType, out var existingViews))
            {
                var results = await _repository.GetByViewAsync(organizationId, definition.ViewType, o => o.PageLimit(1000));
                existingViews = results.Documents.ToList();
                savedViewsByView.Add(definition.ViewType, existingViews);
            }

            var existing = FindPredefinedSavedView(definition, existingViews);
            if (existing is not null)
                existingPredefinedViews.Add(existing);
        }

        return existingPredefinedViews;
    }

    private static SavedView? FindPredefinedSavedView(PredefinedSavedViewDefinition definition, IReadOnlyCollection<SavedView> existingViews)
    {
        return existingViews.FirstOrDefault(view => view.UserId is null && String.Equals(view.PredefinedKey, definition.Key, StringComparison.OrdinalIgnoreCase))
            ?? existingViews.FirstOrDefault(view => view.UserId is null && String.IsNullOrWhiteSpace(view.PredefinedKey) && String.Equals(view.Name.Trim(), definition.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCreatedPredefinedSavedViews(Organization organization)
    {
        if (organization.Data is null || !organization.Data.TryGetValue(PredefinedSavedViewsDataKey, out object? versionValue))
            return false;

        return Int32.TryParse(versionValue?.ToString(), out int version) && version >= PredefinedSavedViewsVersion;
    }

    private SavedView CreatePredefinedSavedView(string organizationId, PredefinedSavedViewDefinition definition, string slug)
    {
        return new SavedView
        {
            OrganizationId = organizationId,
            CreatedByUserId = CurrentUser.Id,
            PredefinedKey = definition.Key,
            Name = definition.Name,
            Slug = slug,
            ViewType = definition.ViewType,
            Filter = definition.Filter,
            Time = definition.Time,
            Sort = definition.Sort,
            FilterDefinitions = PredefinedSavedViewsDataSeed.GetRawJson(definition.FilterDefinitions),
            Columns = Copy(definition.Columns),
            ColumnOrder = definition.ColumnOrder is null ? null : [.. definition.ColumnOrder],
            ShowStats = definition.ShowStats,
            ShowChart = definition.ShowChart,
            Version = 1
        };
    }

    private static bool ApplyPredefinedSavedView(SavedView savedView, PredefinedSavedViewDefinition definition, string slug)
    {
        var changed = false;
        changed |= SetIfChanged(savedView, definition.Key, static (view, value) => view.PredefinedKey = value, static view => view.PredefinedKey);
        changed |= SetIfChanged(savedView, definition.Name, static (view, value) => view.Name = value, static view => view.Name);
        changed |= SetIfChanged(savedView, slug, static (view, value) => view.Slug = value, static view => view.Slug);
        changed |= SetIfChanged(savedView, definition.Filter, static (view, value) => view.Filter = value, static view => view.Filter);
        changed |= SetIfChanged(savedView, definition.Time, static (view, value) => view.Time = value, static view => view.Time);
        changed |= SetIfChanged(savedView, definition.Sort, static (view, value) => view.Sort = value, static view => view.Sort);
        changed |= SetIfChanged(savedView, PredefinedSavedViewsDataSeed.GetRawJson(definition.FilterDefinitions), static (view, value) => view.FilterDefinitions = value, static view => view.FilterDefinitions);
        changed |= SetDictionaryIfChanged(savedView, definition.Columns);
        changed |= SetListIfChanged(savedView, definition.ColumnOrder);
        changed |= SetIfChanged(savedView, definition.ShowStats, static (view, value) => view.ShowStats = value, static view => view.ShowStats);
        changed |= SetIfChanged(savedView, definition.ShowChart, static (view, value) => view.ShowChart = value, static view => view.ShowChart);
        changed |= SetIfChanged(savedView, 1, static (view, value) => view.Version = value, static view => view.Version);

        return changed;
    }

    private async Task<SavedView> UpsertSystemPredefinedSavedViewAsync(SavedView source)
    {
        var existingPredefinedViews = await GetSystemPredefinedSavedViewsAsync(source.ViewType);
        var key = GetPredefinedKey(source);
        var existing = existingPredefinedViews.FirstOrDefault(view => String.Equals(view.PredefinedKey, key, StringComparison.OrdinalIgnoreCase))
            ?? existingPredefinedViews.FirstOrDefault(view => String.IsNullOrWhiteSpace(view.PredefinedKey) && String.Equals(view.Slug, source.Slug, StringComparison.OrdinalIgnoreCase));
        var slug = GetUniqueSlug(source.Slug, existingPredefinedViews, existing?.Id);

        if (existing is null)
        {
            var savedView = CreateSystemPredefinedSavedView(source, key, slug);
            await _repository.AddAsync(savedView, o => o.Cache().ImmediateConsistency());
            return savedView;
        }

        ApplySavedViewConfiguration(existing, source, key, slug);
        existing.UpdatedByUserId = CurrentUser.Id;
        await _repository.SaveAsync(existing, o => o.Cache().ImmediateConsistency());
        return existing;
    }

    private SavedView CreateSystemPredefinedSavedView(SavedView source, string key, string slug)
    {
        var savedView = new SavedView
        {
            OrganizationId = PredefinedSavedViewsDataSeed.SystemOrganizationId,
            CreatedByUserId = CurrentUser.Id,
            Version = 1
        };

        ApplySavedViewConfiguration(savedView, source, key, slug);
        return savedView;
    }

    private static void ApplySavedViewConfiguration(SavedView destination, SavedView source, string key, string slug)
    {
        destination.UserId = null;
        destination.PredefinedKey = key;
        destination.Name = source.Name;
        destination.Slug = slug;
        destination.ViewType = source.ViewType;
        destination.Filter = source.Filter;
        destination.Time = source.Time;
        destination.Sort = source.Sort;
        destination.FilterDefinitions = source.FilterDefinitions;
        destination.Columns = Copy(source.Columns);
        destination.ColumnOrder = source.ColumnOrder is null ? null : [.. source.ColumnOrder];
        destination.ShowStats = source.ShowStats;
        destination.ShowChart = source.ShowChart;
    }

    private async Task<IReadOnlyCollection<PredefinedSavedViewDefinition>> GetPredefinedSavedViewsAsync()
    {
        var definitions = new List<PredefinedSavedViewDefinition>();

        foreach (var viewType in NewSavedView.ValidViewTypes)
        {
            var savedViews = await GetSystemPredefinedSavedViewsAsync(viewType);
            foreach (var savedView in savedViews)
            {
                var key = GetPredefinedKey(savedView);
                definitions.Add(ToPredefinedSavedView(savedView, key));
            }
        }

        return definitions;
    }

    private async Task DeleteSystemPredefinedSavedViewAsync(SavedView source)
    {
        var key = GetPredefinedKey(source);
        var existingPredefinedViews = await GetSystemPredefinedSavedViewsAsync(source.ViewType);
        var existing = existingPredefinedViews.FirstOrDefault(view => String.Equals(view.PredefinedKey, key, StringComparison.OrdinalIgnoreCase))
            ?? existingPredefinedViews.FirstOrDefault(view => String.IsNullOrWhiteSpace(view.PredefinedKey) && String.Equals(view.Slug, source.Slug, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            await _repository.RemoveAsync(existing.Id, o => o.ImmediateConsistency());
    }

    private async Task<List<SavedView>> GetSystemPredefinedSavedViewsAsync(string viewType)
    {
        var results = await _repository.GetByViewAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId, viewType, o => o.PageLimit(1000));
        return results.Documents.Where(view => view.UserId is null).ToList();
    }

    private static PredefinedSavedViewDefinition ToPredefinedSavedView(SavedView savedView, string key)
    {
        return new PredefinedSavedViewDefinition
        {
            Key = key,
            Name = savedView.Name,
            Slug = savedView.Slug,
            ViewType = savedView.ViewType,
            Filter = savedView.Filter,
            Time = savedView.Time,
            Sort = savedView.Sort,
            FilterDefinitions = ParseFilterDefinitions(savedView.FilterDefinitions),
            Columns = savedView.Columns,
            ColumnOrder = savedView.ColumnOrder,
            ShowStats = savedView.ShowStats,
            ShowChart = savedView.ShowChart
        };
    }

    private static JsonElement? ParseFilterDefinitions(string? filterDefinitions)
    {
        if (String.IsNullOrWhiteSpace(filterDefinitions))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(filterDefinitions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetPredefinedKey(SavedView savedView)
    {
        if (!String.IsNullOrWhiteSpace(savedView.PredefinedKey))
            return savedView.PredefinedKey;

        return $"{savedView.ViewType}:{ToSlug(String.IsNullOrWhiteSpace(savedView.Slug) ? savedView.Name : savedView.Slug)}";
    }

    private static bool SetIfChanged<T>(SavedView savedView, T value, Action<SavedView, T> setValue, Func<SavedView, T> getValue)
    {
        if (EqualityComparer<T>.Default.Equals(getValue(savedView), value))
            return false;

        setValue(savedView, value);
        return true;
    }

    private static bool SetDictionaryIfChanged(SavedView savedView, IReadOnlyDictionary<string, bool>? value)
    {
        if (DictionaryEquals(savedView.Columns, value))
            return false;

        savedView.Columns = Copy(value);
        return true;
    }

    private static bool SetListIfChanged(SavedView savedView, IReadOnlyCollection<string>? value)
    {
        if ((savedView.ColumnOrder ?? []).SequenceEqual(value ?? []))
            return false;

        savedView.ColumnOrder = value is null ? null : [.. value];
        return true;
    }

    private static Dictionary<string, bool>? Copy(IReadOnlyDictionary<string, bool>? value)
    {
        return value is null ? null : value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static bool DictionaryEquals(IReadOnlyDictionary<string, bool>? left, IReadOnlyDictionary<string, bool>? right)
    {
        if (left is null)
            return right is null;

        if (right is null)
            return false;

        if (left.Count != right.Count)
            return false;

        return left.All(kvp => right.TryGetValue(kvp.Key, out var value) && value == kvp.Value);
    }

    private static string GetUniqueSlug(string slug, IReadOnlyCollection<SavedView> existingViews, string? excludingId)
    {
        var baseSlug = ToSlug(slug);
        if (String.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "saved-view";

        baseSlug = baseSlug.Length > 100 ? baseSlug[..100].Trim('-') : baseSlug;
        var candidate = baseSlug;
        var suffix = 2;

        while (existingViews.Any(view => view.Id != excludingId && String.Equals(ToFallbackSlug(String.IsNullOrWhiteSpace(view.Slug) ? view.Name : view.Slug, view.Id), candidate, StringComparison.OrdinalIgnoreCase)))
        {
            var suffixText = $"-{suffix}";
            var maxBaseLength = 100 - suffixText.Length;
            candidate = $"{baseSlug[..Math.Min(baseSlug.Length, maxBaseLength)].Trim('-')}{suffixText}";
            suffix++;
        }

        return candidate;
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
