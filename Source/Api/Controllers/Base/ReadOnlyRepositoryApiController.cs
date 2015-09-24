using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Nito.AsyncEx;

namespace Exceptionless.Api.Controllers {
    public abstract class ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> : ExceptionlessApiController where TRepository : IReadOnlyRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);

        public ReadOnlyRepositoryApiController(TRepository repository) {
            _repository = repository;
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
            if (_isOwnedByOrganization && model != null && !await CanAccessOrganizationAsync(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        protected virtual async Task<ICollection<TModel>> GetModelsAsync(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return new List<TModel>();

            var models = (await _repository.GetByIdsAsync(ids, useCache: useCache)).Documents;
            if (!_isOwnedByOrganization)
                return models;

            var results = new List<TModel>();
            foreach (var model in models) {
                if (await CanAccessOrganizationAsync(((IOwnedByOrganization)model).OrganizationId))
                    results.Add(model);
            }

            return results;
        }
        
        #region Mapping

        private static bool _mapsCreated;
        private static readonly AsyncLock _lock = new AsyncLock();
        private async Task EnsureMapsAsync() {
            if (_mapsCreated)
                return;

            using (await _lock.LockAsync()) {
                if (_mapsCreated)
                    return;

                await CreateMapsAsync();

                _mapsCreated = true;
            }
        }
       
        protected virtual Task CreateMapsAsync() {
            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();

            return TaskHelper.Completed();
        }

        protected async Task<TDestination> MapAsync<TDestination>(object source, bool isResult = false) {
            await EnsureMapsAsync();

            var destination = Mapper.Map<TDestination>(source);
            if (isResult)
                AfterResultMap(destination);
            return destination;
        }

        protected async Task<ICollection<TDestination>> MapCollectionAsync<TDestination>(object source, bool isResult = false) {
            await EnsureMapsAsync();

            var destination = Mapper.Map<ICollection<TDestination>>(source);
            if (isResult)
                destination.ForEach(d => AfterResultMap(d));
            return destination;
        }

        protected virtual void AfterResultMap(object model) {
            var dataModel = model as IData;
            dataModel?.Data.RemoveSensitiveData();

            var enumerable = model as IEnumerable;
            if (enumerable == null)
                return;

            foreach (var item in enumerable) {
                var itemDataModel = item as IData;
                itemDataModel?.Data.RemoveSensitiveData();
            }
        }

        #endregion
    }
}