using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Controllers {
    [Authorize(Roles = AuthorizationRoles.User)]
    public abstract class RepositoryApiController<TModel, TViewModel, TRepository> : ExceptionlessApiController
        where TModel : class, IIdentity, new()
        where TViewModel : class, new() 
        where TRepository : MongoRepositoryWithIdentity<TModel> {
        protected readonly TRepository _repository;

        public RepositoryApiController(TRepository repository) {
            _repository = repository;
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

            return Ok(Mapper.Map<TModel, TViewModel>(model));
        }

        protected virtual TModel GetModel(string id) {
            if (String.IsNullOrEmpty(id))
                return null;

            return _repository.GetByIdCached(id);
        }
  
        [Route]
        [HttpPost]
        public virtual IHttpActionResult Post(TModel value) {
            if (!CanAdd(value))
                return NotFound();

            if (value == null)
                return BadRequest();

            TModel model;
            try {
                model = AddModel(value);
            } catch (WriteConcernException) {
                return Conflict();
            }

            return Created(new Uri(Url.Link("DefaultApi", new { id = model.Id })), String.Empty);
        }

        protected virtual bool CanAdd(TModel value) {
            return value != null && String.IsNullOrEmpty(value.Id);
        }

        /// <summary>
        /// Inserts a document.
        /// </summary>
        /// <param name="value">The document.</param>
        protected virtual TModel AddModel(TModel value) {
            return _repository.Add(value);
        }

        [Route]
        [HttpPut]
        [HttpPatch]
        public virtual IHttpActionResult Patch(string id, Delta<TModel> changes) {
            // TODO: Investigate work around for: http://aspnetwebstack.codeplex.com/workitem/562
            if (changes == null || !changes.GetChangedPropertyNames().Any())
                return BadRequest();

            TModel original = GetModel(id);
            if (original == null)
                return NotFound();

            if (!CanUpdate(original, changes))
                return Conflict();

            UpdateModel(original, changes);

            return Ok();
        }

        protected virtual bool CanUpdate(TModel original, Delta<TModel> changes) {
            if (changes.ContainsChangedProperty(t => t.Id) && !String.Equals(original.Id, changes.GetEntity().Id, StringComparison.OrdinalIgnoreCase))
                return false;

            return changes.GetChangedPropertyNames().Any();
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