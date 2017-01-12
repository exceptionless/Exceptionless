using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Api.Controllers {
    public abstract class ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> : ExceptionlessApiController where TRepository : IReadOnlyRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);
        protected static readonly bool _supportsSoftDeletes = typeof(ISupportSoftDeletes).IsAssignableFrom(typeof(TModel));
        protected static readonly IReadOnlyCollection<TModel> EmptyModels = new List<TModel>(0).AsReadOnly();
        protected readonly IMapper _mapper;

        public ReadOnlyRepositoryApiController(TRepository repository, IMapper mapper) {
            _repository = repository;
            _mapper = mapper;
        }

        public virtual async Task<IHttpActionResult> GetByIdAsync(string id) {
            TModel model = await GetModelAsync(id);
            if (model == null)
                return NotFound();

            return await OkModelAsync(model);
        }

        protected async Task<IHttpActionResult> OkModelAsync(TModel model) {
            return Ok(await MapAsync<TViewModel>(model, true));
        }

        protected virtual async Task<TModel> GetModelAsync(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            TModel model = await _repository.GetByIdAsync(id, useCache);
            if (model == null)
                return null;

            if (_supportsSoftDeletes && ((ISupportSoftDeletes)model).IsDeleted)
                return null;

            if (_isOwnedByOrganization && !CanAccessOrganization(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        protected virtual async Task<IReadOnlyCollection<TModel>> GetModelsAsync(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return EmptyModels;

            var models = await _repository.GetByIdsAsync(ids, useCache);
            if (_supportsSoftDeletes)
                models = models.Where(m => !((ISupportSoftDeletes)m).IsDeleted).ToList();

            if (_isOwnedByOrganization)
                models = models.Where(m => CanAccessOrganization(((IOwnedByOrganization)m).OrganizationId)).ToList();

            return models;
        }

        #region Mapping

        protected async Task<TDestination> MapAsync<TDestination>(object source, bool isResult = false) {
            var destination = _mapper.Map<TDestination>(source);
            if (isResult)
                await AfterResultMapAsync(new List<TDestination>(new[] { destination }));

            return destination;
        }

        protected async Task<ICollection<TDestination>> MapCollectionAsync<TDestination>(object source, bool isResult = false) {
            var destination = _mapper.Map<ICollection<TDestination>>(source);
            if (isResult)
                await AfterResultMapAsync<TDestination>(destination);

            return destination;
        }

        protected virtual Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models) {
            foreach (var model in models.OfType<IData>())
                model.Data.RemoveSensitiveData();

            return Task.CompletedTask;
        }

        #endregion
    }
}
