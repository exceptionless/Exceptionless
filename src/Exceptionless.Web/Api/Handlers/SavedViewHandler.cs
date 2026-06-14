using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Web.Api.Handlers;

public class SavedViewHandler(
    ISavedViewRepository repository,
    IOrganizationRepository organizationRepository,
    ILockProvider lockProvider,
    ApiMapper mapper,
    IHttpContextAccessor httpContextAccessor)
{
    private const int MaxViewsPerOrganization = 100;
    private const string PredefinedSavedViewsDataKey = "@@PredefinedSavedViewsVersion";
    private const int PredefinedSavedViewsVersion = 4;

    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<PagedResult<ViewSavedView>>> Handle(GetSavedViewsByOrganization message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        await EnsurePredefinedSavedViewsCreatedAsync(message.OrganizationId);

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var results = await repository.GetByOrganizationForUserAsync(message.OrganizationId, GetCurrentUserId(), o => o.PageNumber(page).PageLimit(limit));
        AppDiagnostics.SavedViewsSize.Add((int)results.Total);

        var viewModels = MapToViewModels(results.Documents);
        return new PagedResult<ViewSavedView>(viewModels, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    public async Task<Result<PagedResult<ViewSavedView>>> Handle(GetSavedViewsByView message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        if (!NewSavedView.ValidViewTypes.Contains(message.ViewType))
            return Result.NotFound("Organization not found.");

        await EnsurePredefinedSavedViewsCreatedAsync(message.OrganizationId);

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var results = await repository.GetByViewForUserAsync(message.OrganizationId, message.ViewType, GetCurrentUserId(), o => o.PageNumber(page).PageLimit(limit));
        AppDiagnostics.SavedViewsViewTypeSize.Add((int)results.Total);

        var viewModels = MapToViewModels(results.Documents);
        return new PagedResult<ViewSavedView>(viewModels, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    public async Task<Result<ViewSavedView>> Handle(GetSavedViewById message)
    {
        var model = await GetModelAsync(message.Id);
        if (model is null)
            return Result.NotFound("Saved view not found.");

        return MapToViewModel(model);
    }

    public async Task<Result<ViewSavedView>> Handle(CreateSavedView message)
    {
        if (!HttpContext.Request.IsInOrganization(message.OrganizationId))
            return Result.BadRequest("Invalid organization.");

        var savedView = message.SavedView;
        savedView.OrganizationId = message.OrganizationId;
        if (savedView.IsPrivate is true)
            savedView.UserId = GetCurrentUserId();

        return await PostImplAsync(savedView);
    }

    public async Task<Result<IReadOnlyCollection<ViewSavedView>>> Handle(CreatePredefinedSavedViews message)
    {
        if (!HttpContext.Request.IsInOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        var savedViews = await UpsertPredefinedSavedViewsAsync(message.OrganizationId);
        return MapToViewModels(savedViews);
    }

    public async Task<Result<IReadOnlyCollection<PredefinedSavedViewDefinition>>> Handle(GetPredefinedSavedViews message)
    {
        var definitions = await GetPredefinedSavedViewsAsync();
        return Result<IReadOnlyCollection<PredefinedSavedViewDefinition>>.Success(definitions);
    }

    public async Task<Result<IReadOnlyCollection<PredefinedSavedViewDefinition>>> Handle(ExportOrganizationSavedViews message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        var definitions = new List<PredefinedSavedViewDefinition>();

        foreach (var viewType in NewSavedView.ValidViewTypes)
        {
            var results = await repository.GetByViewAsync(message.OrganizationId, viewType, o => o.PageLimit(1000));
            foreach (var savedView in results.Documents.Where(v => v.UserId is null))
            {
                var key = GetPredefinedKey(savedView);
                definitions.Add(ToPredefinedSavedView(savedView, key));
            }
        }

        return definitions;
    }

    public async Task<Result<IReadOnlyCollection<PredefinedSavedViewDefinition>>> Handle(ReplacePredefinedSavedViews message)
    {
        foreach (var viewType in NewSavedView.ValidViewTypes)
        {
            var existingViews = await GetSystemPredefinedSavedViewsAsync(viewType);
            if (existingViews.Count > 0)
                await repository.RemoveAsync(existingViews.Select(v => v.Id).ToList(), o => o.ImmediateConsistency());
        }

        var savedViews = message.Definitions.Select(definition => new SavedView
            {
                OrganizationId = PredefinedSavedViewsDataSeed.SystemOrganizationId,
                CreatedByUserId = GetCurrentUserId(),
                PredefinedKey = definition.Key,
                Name = definition.Name,
                Slug = definition.Slug,
                ViewType = definition.ViewType,
                Filter = definition.Filter,
                Time = definition.Time,
                Sort = definition.Sort,
                FilterDefinitions = definition.FilterDefinitions is { } filterDefinitions ? JsonSerializer.Serialize(filterDefinitions) : null,
                Columns = definition.Columns is { } columns ? new Dictionary<string, bool>(columns) : null,
                ColumnOrder = definition.ColumnOrder?.ToList(),
                ShowStats = definition.ShowStats,
                ShowChart = definition.ShowChart,
                Version = 1
            })
            .ToList();

        if (savedViews.Count > 0)
            await repository.AddAsync(savedViews, o => o.Cache().ImmediateConsistency());

        return Result<IReadOnlyCollection<PredefinedSavedViewDefinition>>.Success(await GetPredefinedSavedViewsAsync());
    }

    public async Task<Result<ViewSavedView>> Handle(PromoteToPredefinedSavedView message)
    {
        var source = await repository.GetByIdAsync(message.Id);
        if (source is null)
            return Result.NotFound("Saved view not found.");

        var savedView = await UpsertSystemPredefinedSavedViewAsync(source);
        return MapToViewModel(savedView);
    }

    public async Task<Result> Handle(DeletePredefinedSavedView message)
    {
        var source = await repository.GetByIdAsync(message.Id);
        if (source is null)
            return Result.NotFound("Saved view not found.");

        await DeleteSystemPredefinedSavedViewAsync(source);
        return Result.NoContent();
    }

    public async Task<Result<ViewSavedView>> Handle(UpdateSavedViewMessage message)
    {
        var original = await GetModelAsync(message.Id, useCache: false);
        if (original is null)
            return Result.NotFound("Saved view not found.");

        if (message.PatchDocument.IsEmpty())
            return MapToViewModel(original);

        var validationResult = JsonPatchValidation.ValidateOperations(message.PatchDocument);
        if (!validationResult.IsSuccess)
            return Result<ViewSavedView>.FromResult(validationResult);

        var dto = new UpdateSavedView {
            Name = original.Name,
            Filter = original.Filter,
            Time = original.Time,
            Sort = original.Sort,
            Slug = original.Slug,
            FilterDefinitions = original.FilterDefinitions,
            Columns = original.Columns is not null ? new Dictionary<string, bool>(original.Columns) : null,
            ColumnOrder = original.ColumnOrder is not null ? [.. original.ColumnOrder] : null,
            ShowStats = original.ShowStats,
            ShowChart = original.ShowChart
        };

        var patchResult = JsonPatchValidation.ApplyPatch(message.PatchDocument, dto);
        if (!patchResult.IsSuccess)
            return Result<ViewSavedView>.FromResult(patchResult);

        var error = await CanUpdateAsync(original, dto, message.PatchDocument);
        if (error is not null)
            return error;

        var changedNames = message.PatchDocument.GetAffectedPropertyNames();
        original.Name = dto.Name!;
        original.Filter = dto.Filter;
        original.Time = dto.Time;
        original.Sort = dto.Sort;
        original.Slug = dto.Slug!;
        original.FilterDefinitions = dto.FilterDefinitions;
        original.Columns = dto.Columns is not null ? new Dictionary<string, bool>(dto.Columns) : null;
        original.ColumnOrder = dto.ColumnOrder is not null ? [.. dto.ColumnOrder] : null;
        original.ShowStats = dto.ShowStats;
        original.ShowChart = dto.ShowChart;

        if (changedNames.Contains(nameof(UpdateSavedView.Slug)))
            original.Slug = ToSlug(original.Slug);

        if (String.IsNullOrWhiteSpace(original.Slug))
            original.Slug = ToFallbackSlug(original.Name, original.Id);

        original.UpdatedByUserId = GetCurrentUserId();

        await repository.SaveAsync(original, o => o.Cache());
        return MapToViewModel(original);
    }

    public async Task<Result<ModelActionResults>> Handle(DeleteSavedViews message)
    {
        var items = await GetModelsAsync(message.Ids, useCache: false);
        if (items.Count == 0)
            return Result.NotFound("No saved views found.");

        var results = new ModelActionResults();
        results.AddNotFound(message.Ids.Except(items.Select(i => i.Id)));

        var deletableItems = items.ToList();
        foreach (var model in items)
        {
            var permission = CanDelete(model);
            if (permission.Allowed)
                continue;

            deletableItems.Remove(model);
            results.Failure.Add(permission);
        }

        if (deletableItems.Count == 0)
            return results.Failure.Count == 1 ? Result<ModelActionResults>.FromResult(PermissionToResult(results.Failure.First())) : results;

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return new ModelActionResults();

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return results;
    }

    private async Task<Result<ViewSavedView>> PostImplAsync(NewSavedView value)
    {
        if (value is null)
            return Result.BadRequest("Saved view value is required.");

        var mapped = mapper.MapToSavedView(value);
        mapped.Slug = ToSlug(String.IsNullOrWhiteSpace(mapped.Slug) ? mapped.Name : mapped.Slug);

        if (String.IsNullOrEmpty(mapped.OrganizationId) && HttpContext.Request.GetAssociatedOrganizationIds().Count > 0)
            mapped.OrganizationId = HttpContext.Request.GetDefaultOrganizationId()!;

        var error = await CanAddAsync(mapped);
        if (error is not null)
            return error;

        mapped.CreatedByUserId = GetCurrentUserId();
        mapped.Version = 1;

        var model = await repository.AddAsync(mapped, o => o.Cache());
        var viewModel = MapToViewModel(model);
        return Result<ViewSavedView>.Created(viewModel, $"/api/v2/saved-views/{model.Id}");
    }

    private async Task<Result<ViewSavedView>?> CanAddAsync(SavedView value)
    {
        if (String.IsNullOrEmpty(value.OrganizationId) || !HttpContext.Request.IsInOrganization(value.OrganizationId))
            return Result.Forbidden("Access denied.");

        var count = await repository.CountByOrganizationIdAsync(value.OrganizationId);
        if (count >= MaxViewsPerOrganization)
            return Result.BadRequest($"Organization is limited to {MaxViewsPerOrganization} saved views.");

        if (String.IsNullOrWhiteSpace(value.Slug))
            return Result.Invalid(ValidationError.Create("slug", "URL name cannot be empty. Use at least one letter or number."));

        if (IsReservedSlug(value.Slug))
            return Result.Invalid(ValidationError.Create("general", "URL name cannot look like an event or issue id."));

        if (!IsValidSlug(value.Slug))
            return Result.Invalid(ValidationError.Create("general", "URL name can only contain lowercase letters, numbers, and single dashes."));

        if (await NameExistsAsync(value.OrganizationId, value.ViewType, value.Name, null))
            return Result.Conflict($"A saved view named '{value.Name.Trim()}' already exists.");

        if (await SlugExistsAsync(value.OrganizationId, value.ViewType, value.Slug, null))
            return Result.Conflict($"A saved view with URL name '{value.Slug}' already exists.");

        if (!HttpContext.Request.CanAccessOrganization(value.OrganizationId))
            return Result.Invalid(ValidationError.Create("organization_id", "Invalid organization id specified."));

        return null;
    }

    private async Task<Result<ViewSavedView>?> CanUpdateAsync(SavedView original, UpdateSavedView dto, JsonPatchDocument<UpdateSavedView> patch)
    {
        if (original.UserId is not null && original.UserId != GetCurrentUserId() && !HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return Result.NotFound("Saved view not found.");

        var changedNames = patch.GetAffectedPropertyNames();

        if (changedNames.Contains(nameof(UpdateSavedView.Name)) && String.IsNullOrWhiteSpace(dto.Name))
            return Result.Invalid(ValidationError.Create("name", "Name cannot be empty or whitespace."));

        if (changedNames.Contains(nameof(UpdateSavedView.Slug)) && String.IsNullOrWhiteSpace(dto.Slug))
            return Result.Invalid(ValidationError.Create("slug", "URL name cannot be empty. Use at least one letter or number."));

        if (dto.Name is { Length: > 100 })
            return Result.Invalid(ValidationError.Create("name", "Name cannot exceed 100 characters."));

        if (dto.Slug is { Length: > 100 })
            return Result.Invalid(ValidationError.Create("slug", "Slug cannot exceed 100 characters."));

        if (dto.Filter is { Length: > 2000 })
            return Result.Invalid(ValidationError.Create("filter", "Filter cannot exceed 2000 characters."));

        if (dto.Time is { Length: > 100 })
            return Result.Invalid(ValidationError.Create("time", "Time cannot exceed 100 characters."));

        if (dto.Sort is { Length: > 100 })
            return Result.Invalid(ValidationError.Create("sort", "Sort cannot exceed 100 characters."));

        if (dto.FilterDefinitions is { Length: > SavedView.MaxFilterDefinitionsLength })
        {
            return Result.Invalid(ValidationError.Create("filter_definitions", $"FilterDefinitions cannot exceed {SavedView.MaxFilterDefinitionsLength} characters."));
        }

        if (changedNames.Contains(nameof(UpdateSavedView.Name))
            && dto.Name is string changedName
            && await NameExistsAsync(original.OrganizationId, original.ViewType, changedName, original.Id))
        {
            return Result.Conflict($"A saved view named '{changedName.Trim()}' already exists.");
        }

        if (changedNames.Contains(nameof(UpdateSavedView.Slug)) && dto.Slug is string changedSlug)
        {
            var normalizedSlug = ToSlug(changedSlug);
            if (IsReservedSlug(normalizedSlug))
                return Result.Invalid(ValidationError.Create("general", "URL name cannot look like an event or issue id."));

            if (!IsValidSlug(normalizedSlug))
                return Result.Invalid(ValidationError.Create("general", "URL name can only contain lowercase letters, numbers, and single dashes."));

            if (await SlugExistsAsync(original.OrganizationId, original.ViewType, normalizedSlug, original.Id))
                return Result.Conflict($"A saved view with URL name '{normalizedSlug}' already exists.");
        }

        if (changedNames.Contains(nameof(UpdateSavedView.FilterDefinitions))
            && dto.FilterDefinitions is { Length: > 0 } filterDefs
            && !NewSavedView.IsValidJsonArray(filterDefs))
        {
            return Result.Invalid(ValidationError.Create("filter_definitions", "FilterDefinitions must be a valid JSON array."));
        }

        if (changedNames.Contains(nameof(UpdateSavedView.Columns)) || changedNames.Contains(nameof(UpdateSavedView.ColumnOrder)))
        {
            var validationError = ValidateColumns(original.ViewType, dto);
            if (validationError is not null)
                return Result.Invalid(ValidationError.Create("columns", validationError.ErrorMessage ?? "Invalid column keys."));
        }

        if (!HttpContext.Request.CanAccessOrganization(original.OrganizationId))
            return Result.Invalid(ValidationError.Create("organization_id", "Invalid organization id specified."));

        return null;
    }

    private PermissionResult CanDelete(SavedView value)
    {
        if (value.UserId is not null && value.UserId != GetCurrentUserId() && !HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return PermissionResult.DenyWithNotFound(value.Id);

        if (!HttpContext.Request.CanAccessOrganization(value.OrganizationId))
            return PermissionResult.DenyWithNotFound(value.Id);

        return PermissionResult.Allow;
    }

    private async Task<SavedView?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!String.IsNullOrEmpty(model.OrganizationId) && !HttpContext.Request.IsInOrganization(model.OrganizationId))
            return null;

        if (model.UserId is not null && model.UserId != GetCurrentUserId() && !HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return null;

        return model;
    }

    private async Task<IReadOnlyCollection<SavedView>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var models = await repository.GetByIdsAsync(ids, o => o.Cache(useCache));
        return models.Where(m => HttpContext.Request.CanAccessOrganization(m.OrganizationId)).ToList();
    }

    private ViewSavedView MapToViewModel(SavedView model)
    {
        var viewModel = mapper.MapToViewSavedView(model);
        if (String.IsNullOrWhiteSpace(viewModel.Slug))
            viewModel.Slug = ToFallbackSlug(viewModel.Name, viewModel.Id);

        AfterResultMap([viewModel]);
        return viewModel;
    }

    private List<ViewSavedView> MapToViewModels(IEnumerable<SavedView> models) => models.Select(MapToViewModel).ToList();

    private string GetCurrentUserId() => HttpContext.Request.GetUser().Id;

    private static void AfterResultMap<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();
    }

    private static Result PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode is StatusCodes.Status404NotFound)
            return Result.NotFound(permission.Message ?? "Saved view not found.");

        if (permission.StatusCode is StatusCodes.Status409Conflict)
            return Result.Conflict(permission.Message ?? "Conflict.");

        if (permission.StatusCode is StatusCodes.Status422UnprocessableEntity)
            return Result.Invalid(ValidationError.Create("general", permission.Message ?? "Validation failed."));

        return Result.Forbidden(permission.Message ?? "Access denied.");
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

    // --- Predefined saved views logic ---

    private async Task EnsurePredefinedSavedViewsCreatedAsync(string organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null || HasCreatedPredefinedSavedViews(organization))
            return;

        await UpsertPredefinedSavedViewsAsync(organizationId, true);
    }

    private async Task<IReadOnlyCollection<SavedView>> UpsertPredefinedSavedViewsAsync(string organizationId, bool onlyIfNeverCreated = false)
    {
        List<SavedView> savedViews = [];

        bool lockAcquired = await lockProvider.TryUsingAsync($"predefined-saved-views:{organizationId}", async () =>
        {
            var organization = await organizationRepository.GetByIdAsync(organizationId);
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
            await organizationRepository.SaveAsync(organization, o => o.Cache().ImmediateConsistency());
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
                var results = await repository.GetByViewAsync(organizationId, definition.ViewType, o => o.PageLimit(1000));
                existingViews = results.Documents.ToList();
                savedViewsByView.Add(definition.ViewType, existingViews);
            }

            var existing = FindPredefinedSavedView(definition, existingViews);
            var slug = GetUniqueSlug(definition.Slug, existingViews, existing?.Id);

            if (existing is null)
            {
                var savedView = CreatePredefinedSavedView(organizationId, definition, slug);
                await repository.AddAsync(savedView, o => o.Cache().ImmediateConsistency());
                existingViews.Add(savedView);
                upserted.Add(savedView);
                continue;
            }

            if (ApplyPredefinedSavedView(existing, definition, slug))
            {
                existing.UpdatedByUserId = GetCurrentUserId();
                await repository.SaveAsync(existing, o => o.Cache().ImmediateConsistency());
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
                var results = await repository.GetByViewAsync(organizationId, definition.ViewType, o => o.PageLimit(1000));
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
            CreatedByUserId = GetCurrentUserId(),
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
            await repository.AddAsync(savedView, o => o.Cache().ImmediateConsistency());
            return savedView;
        }

        ApplySavedViewConfiguration(existing, source, key, slug);
        existing.UpdatedByUserId = GetCurrentUserId();
        await repository.SaveAsync(existing, o => o.Cache().ImmediateConsistency());
        return existing;
    }

    private SavedView CreateSystemPredefinedSavedView(SavedView source, string key, string slug)
    {
        var savedView = new SavedView
        {
            OrganizationId = PredefinedSavedViewsDataSeed.SystemOrganizationId,
            CreatedByUserId = GetCurrentUserId(),
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
            await repository.RemoveAsync(existing.Id, o => o.ImmediateConsistency());
    }

    private async Task<List<SavedView>> GetSystemPredefinedSavedViewsAsync(string viewType)
    {
        var results = await repository.GetByViewAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId, viewType, o => o.PageLimit(1000));
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

        while (existingViews.Any(view => view.Id != excludingId && String.Equals(view.Slug, candidate, StringComparison.OrdinalIgnoreCase)))
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
        var results = await repository.GetByViewForUserAsync(organizationId, viewType, GetCurrentUserId(), o => o.PageLimit(1000));
        return results.Documents.Any(view => view.Id != excludingId && String.Equals(ToFallbackSlug(String.IsNullOrWhiteSpace(view.Slug) ? view.Name : view.Slug, view.Id), slug, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> NameExistsAsync(string organizationId, string viewType, string name, string? excludingId)
    {
        var results = await repository.GetByViewForUserAsync(organizationId, viewType, GetCurrentUserId(), o => o.PageLimit(1000));
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

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static bool NextPageExceedsSkipLimit(int page, int limit) => (page + 1) * limit >= 1000;
}
