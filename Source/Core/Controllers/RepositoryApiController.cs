using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Controllers {
    public abstract class RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel> : ExceptionlessApiController
            where TRepository : MongoRepositoryWithIdentity<TModel>
            where TModel : class, IIdentity, new()
            where TViewModel : class, new()
            where TNewModel : class, new()
            where TUpdateModel : class, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization;

        static RepositoryApiController() {
            _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        }

        public RepositoryApiController(TRepository repository) {
            _repository = repository;

            Run.Once(CreateMaps);
        }

        protected virtual void CreateMaps() {
            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();
            if (Mapper.FindTypeMapFor<TNewModel, TModel>() == null)
                Mapper.CreateMap<TNewModel, TModel>();
        }

        #region Get

        public virtual IHttpActionResult Get(string organizationId = null, int page = 1, int pageSize = 10) {
            IMongoQuery query = null;
            if (_isOwnedByOrganization && !String.IsNullOrEmpty(organizationId))
                query = Query.EQ("oid", ObjectId.Parse(organizationId));

            var results = GetEntities<TViewModel>(query, page: page, pageSize: pageSize);
            return Ok(new PagedResult<TViewModel>(results) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            });
        }

        protected List<T> GetEntities<T>(IMongoQuery query = null, IMongoFields fields = null, int page = 1, int pageSize = 10) {
            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            // filter by the associated organizations
            if (_isOwnedByOrganization) {
                if (query != null)
                    query = Query.And(query, Query.In("oid", User.GetAssociatedOrganizationIds().Select(id => new BsonObjectId(new ObjectId(id)))));
                else
                    query = Query.In("oid", User.GetAssociatedOrganizationIds().Select(id => new BsonObjectId(new ObjectId(id))));
            }

            var cursor = _repository.Collection.Find(query ?? Query.Null).SetSkip(skip).SetLimit(pageSize);
            if (fields != null)
                cursor.SetFields(fields);

            if (typeof(T) == typeof(TModel))
                return cursor.Cast<T>().ToList();

            return cursor.Select(Mapper.Map<TModel, T>).ToList();
        }
        
        public virtual IHttpActionResult GetById(string id) {
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

            var model = _repository.GetByIdCached(id);
            if (_isOwnedByOrganization && model != null && !User.IsInOrganization(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        #endregion

        #region Post

        public virtual IHttpActionResult Post(TNewModel value) {
            if (value == null)
                return BadRequest();

            var orgModel = value as IOwnedByOrganization;
            // if no organization id is specified, default to the user's 1st associated org.
            if (orgModel != null && String.IsNullOrEmpty(orgModel.OrganizationId) && User.GetAssociatedOrganizationIds().Any())
                orgModel.OrganizationId = User.GetDefaultOrganizationId();

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

            var viewModel = Mapper.Map<TModel, TViewModel>(model);
            return Created(GetEntityLink(model.Id), viewModel);
        }

        protected virtual Uri GetEntityLink(string id) {
            return new Uri(Url.Link(String.Format("Get{0}ById", typeof(TModel).Name), new { id }));
        }

        protected virtual PermissionResult CanAdd(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel == null)
                return PermissionResult.Allow;

            if (!User.IsInOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithResult(BadRequest("Invalid organization id specified."));

            return PermissionResult.Allow;
        }

        protected virtual TModel AddModel(TModel value) {
            return _repository.Add(value);
        }

        #endregion

        #region Patch

        public virtual IHttpActionResult Patch(string id, Delta<TUpdateModel> changes) {
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

        protected virtual PermissionResult CanUpdate(TModel original, Delta<TUpdateModel> changes) {
            var orgModel = original as IOwnedByOrganization;
            if (orgModel != null && !User.IsInOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithResult(BadRequest("Invalid organization id specified."));

            return PermissionResult.Allow;
        }

        protected virtual TModel UpdateModel(TModel original, Delta<TUpdateModel> changes) {
            changes.Patch(original);
            return _repository.Update(original);
        }

        #endregion

        #region Delete

        public virtual IHttpActionResult Delete(string id) {
            TModel item = GetModel(id);
            if (item == null)
                return BadRequest();

            var permission = CanDelete(item);
            if (!permission.Allowed)
                return permission.HttpActionResult ?? NotFound();

            DeleteModel(item);
            return StatusCode(HttpStatusCode.NoContent);
        }

        protected virtual PermissionResult CanDelete(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel != null && !User.IsInOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithResult(NotFound());

            return PermissionResult.Allow;
        }

        protected virtual void DeleteModel(TModel value) {
            _repository.Delete(value);
        }

        #endregion
    }
}