using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Helpers;
using Exceptionless.Core.Models;
using FluentValidation;
using MongoDB.Driver;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    public abstract class RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel> : ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel>
            where TRepository : IRepository<TModel>
            where TModel : class, IIdentity, new()
            where TViewModel : class, IIdentity, new()
            where TNewModel : class, new()
            where TUpdateModel : class, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);

        public RepositoryApiController(TRepository repository) : base(repository) {}

        public virtual async Task<IHttpActionResult> PostAsync(TNewModel value) {
            if (value == null)
                return BadRequest();

            var orgModel = value as IOwnedByOrganization;
            // if no organization id is specified, default to the user's 1st associated org.
            if (!_isOrganization && orgModel != null && String.IsNullOrEmpty(orgModel.OrganizationId) && GetAssociatedOrganizationIds().Any())
                orgModel.OrganizationId = GetDefaultOrganizationId();

            TModel mapped = Map<TModel>(value);
            var permission = CanAdd(mapped);
            if (!permission.Allowed)
                return Permission(permission);

            TModel model;
            try {
                model = AddModel(mapped);
                await AfterAddAsync(model);
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return Created(new Uri(GetEntityLink(model.Id)), Map<TViewModel>(model, true));
        }

        protected async Task<IHttpActionResult> UpdateModelAsync(string id, Func<TModel, TModel> modelUpdateFunc) {
            TModel model = GetModel(id);
            if (model == null)
                return NotFound();

            model = modelUpdateFunc(model);
            _repository.Save(model);
            await AfterUpdateAsync(model);

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(model);

            return Ok(Map<TViewModel>(model, true));
        }

        protected async Task<IHttpActionResult> UpdateModelsAsync(string[] ids, Func<TModel, TModel> modelUpdateFunc) {
            var models = GetModels(ids);
            if (models == null || models.Count == 0)
                return NotFound();

            models.ForEach(m => modelUpdateFunc(m));
            _repository.Save(models);
            models.ForEach(async m => await AfterUpdateAsync(m));
            
            if (typeof(TViewModel) == typeof(TModel))
                return Ok(models);

            return Ok(Map<TViewModel>(models, true));
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

            if (!CanAccessOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            return PermissionResult.Allow;
        }

        protected virtual TModel AddModel(TModel value) {
            return _repository.Add(value);
        }

        protected virtual Task<TModel> AfterAddAsync(TModel value)
        {
            return Task.FromResult(value);
        }

        protected virtual Task<TModel> AfterUpdateAsync(TModel value)
        {
            return Task.FromResult(value);
        }

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
                UpdateModelAsync(original, changes);
                AfterPatchAsync(original);
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return Ok();
        }

        protected virtual PermissionResult CanUpdate(TModel original, Delta<TUpdateModel> changes) {
            var orgModel = original as IOwnedByOrganization;
            if (orgModel != null && !CanAccessOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
                return PermissionResult.DenyWithMessage("OrganizationId cannot be modified.");
            
            return PermissionResult.Allow;
        }

        protected virtual TModel UpdateModelAsync(TModel original, Delta<TUpdateModel> changes) {
            changes.Patch(original);
            return _repository.Save(original);
        }

        protected virtual Task<TModel> AfterPatchAsync(TModel value) {
            return Task.FromResult(value);
        }

        public virtual async Task<IHttpActionResult> DeleteAsync(string[] ids) {
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
                await DeleteModels(items);
            } catch (Exception ex){
                Log.Error().Exception(ex).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();
                return StatusCode(HttpStatusCode.InternalServerError);
            }

            if (results.Failure.Count == 0)
                return StatusCode(HttpStatusCode.NoContent);

            results.Success.AddRange(items.Select(i => i.Id));
            return BadRequest(results);
        }

        protected virtual PermissionResult CanDelete(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel != null && !CanAccessOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return PermissionResult.Allow;
        }

        protected virtual Task DeleteModels(ICollection<TModel> values) {
            _repository.Remove(values);
            return Task.FromResult(0);
        }

        protected override void CreateMaps() {
            base.CreateMaps();

            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();
            if (Mapper.FindTypeMapFor<TNewModel, TModel>() == null)
                Mapper.CreateMap<TNewModel, TModel>();
        }
    }
}