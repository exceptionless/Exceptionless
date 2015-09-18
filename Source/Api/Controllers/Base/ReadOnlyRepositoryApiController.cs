using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Nito.AsyncEx;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    public abstract class ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> : ExceptionlessApiController where TRepository : IReadOnlyRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);

        public ReadOnlyRepositoryApiController(TRepository repository) {
            _repository = repository;
        }

        public virtual async Task<IHttpActionResult> GetByIdAsync(string id) {
            TModel model = await GetModelAsync(id).AnyContext();
            if (model == null)
                return NotFound();

            return await OkModelAsync(model).AnyContext();
        }

        protected async Task<IHttpActionResult> OkModelAsync(TModel model) {
            return Ok(await MapAsync<TViewModel>(model, true).AnyContext());
        }

        protected virtual async Task<TModel> GetModelAsync(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            TModel model = await _repository.GetByIdAsync(id, useCache).AnyContext();
            if (_isOwnedByOrganization && model != null && !CanAccessOrganization(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        protected virtual async Task<ICollection<TModel>> GetModelsAsync(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return new List<TModel>();

            var models = (await _repository.GetByIdsAsync(ids, useCache: useCache).AnyContext()).Documents;
            if (_isOwnedByOrganization)
                models = models?.Where(m => CanAccessOrganization(((IOwnedByOrganization)m).OrganizationId)).ToList();

            return models;
        }

        public virtual Task<IHttpActionResult> GetAsync(string userFilter = null, string query = null, string sort = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternalAsync(null, userFilter, query, sort, offset, mode, page, limit);
        }

        public async Task<IHttpActionResult> GetInternalAsync(string systemFilter = null, string userFilter = null, string query = null, string sort = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page + 1, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var processResult = QueryProcessor.Process(query);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetSystemFilter(processResult.UsesPremiumFeatures, HasOrganizationFilter(query));

            var sortBy = GetSort(sort);
            var options = new PagingOptions {
                Page = page,
                Limit = limit
            };

            FindResults<TModel> models;
            try {
                models = await _repository.GetBySearchAsync(systemFilter, userFilter, query, sortBy.Item1, sortBy.Item2, options).AnyContext();
            } catch (ApplicationException ex) {
                Log.Error().Exception(ex).Property("Search Filter", new {
                    SystemFilter = systemFilter,
                    UserFilter = userFilter,
                    Sort = sort,
                    Offset = offset,
                    Page = page,
                    Limit = limit
                }).Tag("Search").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(await MapCollectionAsync<TViewModel>(models.Documents, true).AnyContext(), options.HasMore, page, models.Total);

            return OkWithResourceLinks(await MapCollectionAsync<TViewModel>(models.Documents, true).AnyContext(), options.HasMore && !NextPageExceedsSkipLimit(page, limit), page, models.Total);
        }
        
        #region Mapping

        private static bool _mapsCreated = false;
        private static readonly AsyncLock _lock = new AsyncLock();
        private async Task EnsureMapsAsync() {
            if (_mapsCreated)
                return;

            using (await _lock.LockAsync()) {
                if (_mapsCreated)
                    return;

                await CreateMapsAsync().AnyContext();

                _mapsCreated = true;
            }
        }
       
        protected virtual Task CreateMapsAsync() {
            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();

            return TaskHelper.Completed();
        }

        protected async Task<TDestination> MapAsync<TDestination>(object source, bool isResult = false) {
            await EnsureMapsAsync().AnyContext();

            var destination = Mapper.Map<TDestination>(source);
            if (isResult)
                AfterResultMap(destination);
            return destination;
        }

        protected async Task<ICollection<TDestination>> MapCollectionAsync<TDestination>(object source, bool isResult = false) {
            await EnsureMapsAsync().AnyContext();

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