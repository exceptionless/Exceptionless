using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Controllers {
    [Authorize(Roles = AuthorizationRoles.User)]
    public abstract class RepositoryApiController<TModel, TViewModel, TNewModel, TRepository> : ExceptionlessApiController
        where TModel : class, IIdentity, new()
        where TViewModel : class, new()
        where TNewModel : class, new() 
        where TRepository : MongoRepositoryWithIdentity<TModel> {
        protected readonly TRepository _repository;

        public RepositoryApiController(TRepository repository) {
            _repository = repository;
            CreateMaps();
        }

        protected virtual void CreateMaps() {
            Mapper.CreateMap<TModel, TViewModel>();
            Mapper.CreateMap<TNewModel, TModel>();
        }

        [Route]
        [HttpGet]
        public virtual IHttpActionResult Get(int page = 1, int pageSize = 10) {
            var results = GetEntities<TViewModel>(page: page, pageSize: pageSize);
            return Ok(new PagedResult<TViewModel>(results) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            });
        }

        protected List<T> GetEntities<T>(IMongoQuery query = null, IMongoFields fields = null, int page = 1, int pageSize = 10) {
            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            var cursor = _repository.Collection.Find(query ?? Query.Null).SetSkip(skip).SetLimit(pageSize);
            if (fields != null)
                cursor.SetFields(fields);

            if (typeof(T) == typeof(TModel))
                return cursor.Cast<T>().ToList();

            return cursor.Select(Mapper.Map<TModel, T>).ToList();
        }
        
        [HttpGet]
        [Route("{id}")]
        public virtual IHttpActionResult Get(string id) {
            TModel model = GetModel(id);
            if (model == null)
                return NotFound();

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(model);

            return Ok(Mapper.Map<TModel, TViewModel>(model));
        }

        protected virtual TModel GetModel(string id) {
            if (String.IsNullOrEmpty(id))
                return null;

            return _repository.GetByIdCached(id);
        }
  
        [Route]
        [HttpPost]
        public virtual IHttpActionResult Post(TNewModel value) {
            if (value == null)
                return BadRequest();

            var mapped = Mapper.Map<TNewModel, TModel>(value);
            var permission = CanAdd(mapped);
            if (!permission.Allowed)
                return permission.HttpActionResult ?? BadRequest();

            TModel model;
            try {
                model = AddModel(mapped);
            } catch (WriteConcernException) {
                return Conflict();
            }

            // TODO: Verify that the link is correct and that it doesn't require a config route.
            var viewModel = Mapper.Map<TModel, TViewModel>(model);
            return Created(new Uri(Url.Link("DefaultApi", new { id = model.Id })), viewModel);
        }

        protected virtual PermissionResult CanAdd(TModel value) {
            return PermissionResult.Allow;
        }

        protected virtual TModel AddModel(TModel value) {
            return _repository.Add(value);
        }

        [Route("{id}")]
        [HttpPut]
        [HttpPatch]
        public virtual IHttpActionResult Patch(string id, Delta<TModel> changes) {
            // if there are no changes in the delta, then ignore the request
            if (changes == null || !changes.GetChangedPropertyNames().Any())
                return Ok();
            
            TModel original = GetModel(id);
            if (original == null)
                return NotFound();

            var permission = CanUpdate(original, changes);
            if (!permission.Allowed)
                return permission.HttpActionResult ?? BadRequest();

            UpdateModel(original, changes);

            return Ok();
        }

        protected virtual string[] GetUpdatablePropertyNames() {
            return new string[] {};
        }

        protected virtual PermissionResult CanUpdate(TModel original, Delta<TModel> changes) {
            string[] unauthorizedProperties = changes.GetChangedPropertyNames(original).Where(p => !GetUpdatablePropertyNames().Contains(p)).ToArray();
            if (unauthorizedProperties.Length == 0)
                return PermissionResult.Allow;

            return new PermissionResult {
                Allowed = false,
                HttpActionResult = BadRequest(String.Format("The following properties can't be changed: {0}", String.Join(", ", unauthorizedProperties)))
            };
        }

        protected virtual TModel UpdateModel(TModel original, Delta<TModel> changes) {
            changes.Patch(original);
            return _repository.Update(original);
        }

        [HttpDelete]
        [Route("{id}")]
        public virtual IHttpActionResult Delete(string id) {
            TModel item = GetModel(id);
            if (item == null)
                return BadRequest();

            if (!CanDelete(item))
                return StatusCode(HttpStatusCode.Unauthorized);

            DeleteModel(item);
            return Ok();
        }

        protected virtual bool CanDelete(TModel value) {
            return true;
        }

        protected virtual void DeleteModel(TModel value) {
            _repository.Delete(value);
        }
    }
}