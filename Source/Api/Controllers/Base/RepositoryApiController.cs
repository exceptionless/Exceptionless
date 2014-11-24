using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using CodeSmith.Core.Helpers;
using Exceptionless.Api.Utility.Results;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Models;
using FluentValidation;
using MongoDB.Driver;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    public abstract class RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel> : ExceptionlessApiController
            where TRepository : IRepository<TModel>
            where TModel : class, IIdentity, new()
            where TViewModel : class, IIdentity, new()
            where TNewModel : class, new()
            where TUpdateModel : class, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);

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

        public virtual IHttpActionResult GetById(string id) {
            TModel model = GetModel(id);
            if (model == null)
                return NotFound();

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(model);

            return Ok(Mapper.Map<TModel, TViewModel>(model));
        }

        protected virtual TModel GetModel(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            TModel model = _repository.GetById(id, useCache);
            if (_isOwnedByOrganization && model != null && !IsInOrganization(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        protected virtual ICollection<TModel> GetModels(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return new List<TModel>();

            ICollection<TModel> models = _repository.GetByIds(ids, useCache: useCache);
            if (_isOwnedByOrganization && models != null)
                models = models.Where(m => IsInOrganization(((IOwnedByOrganization)m).OrganizationId)).ToList();

            return models;
        }

        #endregion

        #region Post

        public virtual IHttpActionResult Post(TNewModel value) {
            if (value == null)
                return BadRequest();

            var orgModel = value as IOwnedByOrganization;
            // if no organization id is specified, default to the user's 1st associated org.
            if (!_isOrganization && orgModel != null && String.IsNullOrEmpty(orgModel.OrganizationId) && GetAssociatedOrganizationIds().Any())
                orgModel.OrganizationId = GetDefaultOrganizationId();

            TModel mapped = Mapper.Map<TNewModel, TModel>(value);
            var permission = CanAdd(mapped);
            if (!permission.Allowed)
                return Permission(permission);

            TModel model;
            try {
                model = AddModel(mapped);
            } catch (WriteConcernException) {
                return Conflict();
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            var viewModel = Mapper.Map<TModel, TViewModel>(model);
            return Created(new Uri(GetEntityLink(model.Id)), viewModel);
        }

        protected virtual string GetEntityLink(string id) {
            return Url.Link(String.Format("Get{0}ById", typeof(TModel).Name), new { id });
        }

        protected virtual string GetEntityResourceLink(string id, string type) {
            return GetResourceLink(Url.Link(String.Format("Get{0}ById", typeof(TModel).Name), new { id }), type);
        }

        protected virtual string GetEntityLink<TEntityType>(string id) {
            return Url.Link(String.Format("Get{0}ById", typeof(TEntityType).Name), new { id });
        }

        protected virtual string GetEntityResourceLink<TEntityType>(string id, string type) {
            return GetResourceLink(Url.Link(String.Format("Get{0}ById", typeof(TEntityType).Name), new { id }), type);
        }

        protected virtual PermissionResult CanAdd(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (_isOrganization || orgModel == null)
                return PermissionResult.Allow;

            if (!IsInOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

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
            
            TModel original = GetModel(id, false);
            if (original == null)
                return NotFound();

            var permission = CanUpdate(original, changes);
            if (!permission.Allowed)
                return Permission(permission);

            try {
                UpdateModel(original, changes);
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return Ok();
        }

        protected virtual PermissionResult CanUpdate(TModel original, Delta<TUpdateModel> changes) {
            var orgModel = original as IOwnedByOrganization;
            if (orgModel != null && !IsInOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            // TODO: The changes might actually change the organization id.

            return PermissionResult.Allow;
        }

        protected virtual TModel UpdateModel(TModel original, Delta<TUpdateModel> changes) {
            changes.Patch(original);
            return _repository.Save(original);
        }

        #endregion

        #region Delete

        public virtual IHttpActionResult Delete(string[] ids) {
            var items = GetModels(ids, false);
            if (!items.Any())
                return NotFound();

            var results = new ModelActionResults();
            results.AddNotFound(ids.Except(items.Select(i => i.Id)));

            foreach (var model in items.ToList()) {
                var permission = CanDelete(model);
                if (permission.Allowed)
                    continue;

                items.Remove(model);
                results.Failure.Add(permission);
            }

            if (items.Count == 0)
                return results.Failure.Count == 1 ? Permission(results.Failure.First()) : BadRequest(results);

            try {
                DeleteModels(items);
            } catch (Exception ex){
                Log.Error().Exception(ex).Report().Write();
                return StatusCode(HttpStatusCode.InternalServerError);
            }

            if (results.Failure.Count == 0)
                return StatusCode(HttpStatusCode.NoContent);

            results.Success.AddRange(items.Select(i => i.Id));
            return BadRequest(results);
        }

        protected virtual PermissionResult CanDelete(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel != null && !IsInOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return PermissionResult.Allow;
        }

        protected virtual void DeleteModels(ICollection<TModel> values) {
            _repository.Remove(values);
        }

        #endregion
    }
}