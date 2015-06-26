using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    public abstract class ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> : ExceptionlessApiController where TRepository : IReadOnlyRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);

        public ReadOnlyRepositoryApiController(TRepository repository) {
            _repository = repository;
        }

        public virtual IHttpActionResult GetById(string id) {
            TModel model = GetModel(id);
            if (model == null)
                return NotFound();

            return OkModel(model);
        }

        protected IHttpActionResult OkModel(TModel model) {
            return Ok(Map<TViewModel>(model, true));
        }

        protected virtual TModel GetModel(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            TModel model = _repository.GetById(id, useCache);
            if (_isOwnedByOrganization && model != null && !CanAccessOrganization(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        protected virtual ICollection<TModel> GetModels(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return new List<TModel>();

            var models = _repository.GetByIds(ids, useCache: useCache).Documents;
            if (_isOwnedByOrganization && models != null)
                models = models.Where(m => CanAccessOrganization(((IOwnedByOrganization)m).OrganizationId)).ToList();

            return models;
        }

        public virtual IHttpActionResult Get(string userFilter = null, string query = null, string sort = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, userFilter, query, sort, offset, mode, page, limit);
        }

        public IHttpActionResult GetInternal(string systemFilter = null, string userFilter = null, string query = null, string sort = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
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
                models = _repository.GetBySearch(systemFilter, userFilter, query, sortBy.Item1, sortBy.Item2, options);
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
                return OkWithResourceLinks(MapCollection<TViewModel>(models.Documents, true), options.HasMore, page, models.Total);

            return OkWithResourceLinks(MapCollection<TViewModel>(models.Documents, true), options.HasMore && !NextPageExceedsSkipLimit(page, limit), page, models.Total);
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();
        }
    }
}