using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

public abstract class RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel> : ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel>
    where TRepository : ISearchableRepository<TModel>
    where TModel : class, IIdentity, new()
    where TViewModel : class, IIdentity, new()
    where TNewModel : class, new()
    where TUpdateModel : class, new()
{
    public RepositoryApiController(TRepository repository, ApiMapper mapper, IAppQueryValidator validator,
        TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(repository, mapper, validator, timeProvider, loggerFactory) { }

    /// <summary>
    /// Maps a new model (from API input) to a domain model. Override in derived controllers.
    /// </summary>
    protected abstract TModel MapToModel(TNewModel newModel);

    protected async Task<ActionResult<TViewModel>> PostImplAsync(TNewModel value)
    {
        if (value is null)
            return BadRequest();

        var mapped = MapToModel(value);
        // if no organization id is specified, default to the user's 1st associated org.
        if (!_isOrganization && mapped is IOwnedByOrganization orgModel && String.IsNullOrEmpty(orgModel.OrganizationId) && GetAssociatedOrganizationIds().Count > 0)
            orgModel.OrganizationId = Request.GetDefaultOrganizationId()!;

        var permission = await CanAddAsync(mapped);
        if (!permission.Allowed)
            return Permission(permission);

        var model = await AddModelAsync(mapped);
        await AfterAddAsync(model);

        var viewModel = MapToViewModel(model);
        await AfterResultMapAsync([viewModel]);
        return Created(new Uri(GetEntityLink(model.Id) ?? throw new InvalidOperationException()), viewModel);
    }

    protected async Task<ActionResult<TViewModel>> UpdateModelAsync(string id, Func<TModel, Task<TModel>> modelUpdateFunc)
    {
        var model = await GetModelAsync(id);
        if (model is null)
            return NotFound();

        if (modelUpdateFunc is not null)
            model = await modelUpdateFunc(model);

        await _repository.SaveAsync(model, o => o.Cache());
        await AfterUpdateAsync(model);

        if (typeof(TViewModel) == typeof(TModel))
            return Ok(model);

        var viewModel = MapToViewModel(model);
        await AfterResultMapAsync([viewModel]);
        return Ok(viewModel);
    }

    protected async Task<ActionResult<TViewModel>> UpdateModelsAsync(string[] ids, Func<TModel, Task<TModel>> modelUpdateFunc)
    {
        var models = await GetModelsAsync(ids, false);
        if (models is null || models.Count == 0)
            return NotFound();

        if (modelUpdateFunc is not null)
            foreach (var model in models)
                await modelUpdateFunc(model);

        await _repository.SaveAsync(models, o => o.Cache());
        foreach (var model in models)
            await AfterUpdateAsync(model);

        if (typeof(TViewModel) == typeof(TModel))
            return Ok(models);

        var viewModels = MapToViewModels(models);
        await AfterResultMapAsync(viewModels);
        return Ok(viewModels);
    }

    protected virtual string? GetEntityLink(string id)
    {
        return Url.Link($"Get{typeof(TModel).Name}ById", new
        {
            id
        });
    }

    protected virtual string? GetEntityResourceLink(string? id, string type)
    {
        return GetResourceLink(Url.Link($"Get{typeof(TModel).Name}ById", new
        {
            id
        }), type);
    }

    protected virtual string? GetEntityLink<TEntityType>(string id)
    {
        return Url.Link($"Get{typeof(TEntityType).Name}ById", new
        {
            id
        });
    }

    protected virtual string? GetEntityResourceLink<TEntityType>(string id, string type)
    {
        return GetResourceLink(Url.Link($"Get{typeof(TEntityType).Name}ById", new
        {
            id
        }), type);
    }

    protected virtual Task<PermissionResult> CanAddAsync(TModel value)
    {
        if (_isOrganization || !(value is IOwnedByOrganization orgModel))
            return Task.FromResult(PermissionResult.Allow);

        if (!CanAccessOrganization(orgModel.OrganizationId))
            return Task.FromResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

        return Task.FromResult(PermissionResult.Allow);
    }

    protected virtual Task<TModel> AddModelAsync(TModel value)
    {
        return _repository.AddAsync(value, o => o.Cache());
    }

    protected virtual Task<TModel> AfterAddAsync(TModel value)
    {
        return Task.FromResult(value);
    }

    protected virtual Task<TModel> AfterUpdateAsync(TModel value)
    {
        return Task.FromResult(value);
    }

    protected async Task<ActionResult<TViewModel>> PatchImplAsync(string id, Delta<TUpdateModel> changes)
    {
        var original = await GetModelAsync(id, false);
        if (original is null)
            return NotFound();

        // if there are no changes in the delta, then ignore the request
        if (!changes.GetChangedPropertyNames().Any())
            return await OkModelAsync(original);

        var permission = await CanUpdateAsync(original, changes);
        if (!permission.Allowed)
            return Permission(permission);

        await UpdateModelAsync(original, changes);
        await AfterPatchAsync(original);

        return await OkModelAsync(original);
    }

    protected virtual Task<PermissionResult> CanUpdateAsync(TModel original, Delta<TUpdateModel> changes)
    {
        if (original is IOwnedByOrganization orgModel && !CanAccessOrganization(orgModel.OrganizationId))
            return Task.FromResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

        if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
            return Task.FromResult(PermissionResult.DenyWithMessage("OrganizationId cannot be modified."));

        return Task.FromResult(PermissionResult.Allow);
    }

    protected virtual Task<TModel> UpdateModelAsync(TModel original, Delta<TUpdateModel> changes)
    {
        changes.Patch(original);
        return _repository.SaveAsync(original, o => o.Cache());
    }

    protected virtual Task<TModel> AfterPatchAsync(TModel value)
    {
        return Task.FromResult(value);
    }

    protected async Task<ActionResult<WorkInProgressResult>> DeleteImplAsync(string[] ids)
    {
        var items = await GetModelsAsync(ids, false);
        if (items.Count == 0)
            return NotFound();

        var results = new ModelActionResults();
        results.AddNotFound(ids.Except(items.Select(i => i.Id)));

        var list = items.ToList();
        foreach (var model in items)
        {
            var permission = await CanDeleteAsync(model);
            if (permission.Allowed)
                continue;

            list.Remove(model);
            results.Failure.Add(permission);
        }

        if (list.Count == 0)
            return results.Failure.Count == 1 ? Permission(results.Failure.First()) : BadRequest(results);

        var workIds = await DeleteModelsAsync(list);
        if (results.Failure.Count == 0)
            return WorkInProgress(workIds);

        results.Workers.AddRange(workIds);
        results.Success.AddRange(list.Select(i => i.Id));
        return BadRequest(results);
    }

    protected virtual Task<PermissionResult> CanDeleteAsync(TModel value)
    {
        if (value is IOwnedByOrganization orgModel && !CanAccessOrganization(orgModel.OrganizationId))
            return Task.FromResult(PermissionResult.DenyWithNotFound(value.Id));

        return Task.FromResult(PermissionResult.Allow);
    }

    protected virtual async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<TModel> values)
    {
        if (_supportsSoftDeletes)
        {
            values.Cast<ISupportSoftDeletes>().ForEach(v => v.IsDeleted = true);
            await _repository.SaveAsync(values);
        }
        else
        {
            await _repository.RemoveAsync(values);
        }

        return [];
    }
}
