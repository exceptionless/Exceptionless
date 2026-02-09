using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Web.Mapping;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

public abstract class ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> : ExceptionlessApiController
    where TRepository : ISearchableReadOnlyRepository<TModel>
    where TModel : class, IIdentity, new()
    where TViewModel : class, IIdentity, new()
{
    protected readonly TRepository _repository;
    protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
    protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);
    protected static readonly bool _supportsSoftDeletes = typeof(ISupportSoftDeletes).IsAssignableFrom(typeof(TModel));
    protected static readonly IReadOnlyCollection<TModel> EmptyModels = new List<TModel>(0).AsReadOnly();
    protected readonly ApiMapper _mapper;
    protected readonly IAppQueryValidator _validator;
    protected readonly ILogger _logger;

    public ReadOnlyRepositoryApiController(TRepository repository, ApiMapper mapper, IAppQueryValidator validator, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(timeProvider)
    {
        _repository = repository;
        _mapper = mapper;
        _validator = validator;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    protected async Task<ActionResult<TViewModel>> GetByIdImplAsync(string id)
    {
        var model = await GetModelAsync(id);
        if (model is null)
            return NotFound();

        return await OkModelAsync(model);
    }

    protected virtual async Task<ActionResult<TViewModel>> OkModelAsync(TModel model)
    {
        var viewModel = MapToViewModel(model);
        await AfterResultMapAsync([viewModel]);
        return Ok(viewModel);
    }

    /// <summary>
    /// Maps a domain model to a view model. Override in derived controllers.
    /// </summary>
    protected abstract TViewModel MapToViewModel(TModel model);

    /// <summary>
    /// Maps a collection of domain models to view models. Override in derived controllers.
    /// </summary>
    protected abstract List<TViewModel> MapToViewModels(IEnumerable<TModel> models);

    protected virtual async Task<TModel?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await _repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (_isOwnedByOrganization && !CanAccessOrganization(((IOwnedByOrganization)model).OrganizationId))
            return null;

        return model;
    }

    protected virtual async Task<IReadOnlyCollection<TModel>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (ids.Length == 0)
            return EmptyModels;

        var models = await _repository.GetByIdsAsync(ids, o => o.Cache(useCache));

        if (_isOwnedByOrganization)
            models = models.Where(m => CanAccessOrganization(((IOwnedByOrganization)m).OrganizationId)).ToList();

        return models;
    }

    protected virtual Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();

        return Task.CompletedTask;
    }
}
