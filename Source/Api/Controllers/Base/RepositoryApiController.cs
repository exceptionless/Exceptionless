using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using FluentValidation;
using NLog.Fluent;

#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    public abstract class RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel> : ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> where TRepository : IRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() where TNewModel : class, new() where TUpdateModel : class, new() {
        public RepositoryApiController(TRepository repository) : base(repository) {}

        public virtual async Task<IHttpActionResult> PostAsync(TNewModel value) {
            if (value == null)
                return BadRequest();

            var orgModel = value as IOwnedByOrganization;
            // if no organization id is specified, default to the user's 1st associated org.
            if (!_isOrganization && orgModel != null && String.IsNullOrEmpty(orgModel.OrganizationId) && GetAssociatedOrganizationIds().Any())
                orgModel.OrganizationId = GetDefaultOrganizationId();

            TModel mapped = await MapAsync<TModel>(value).AnyContext();
            var permission = await CanAddAsync(mapped).AnyContext();
            if (!permission.Allowed)
                return Permission(permission);

            TModel model;
            try {
                model = await AddModelAsync(mapped).AnyContext();
                await AfterAddAsync(model).AnyContext();
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return Created(new Uri(GetEntityLink(model.Id)), await MapAsync<TViewModel>(model, true).AnyContext());
        }

        protected async Task<IHttpActionResult> UpdateModelAsync(string id, Func<TModel, TModel> modelUpdateFunc) {
            TModel model = await GetModelAsync(id).AnyContext();
            if (model == null)
                return NotFound();

            if (modelUpdateFunc != null)
                model = modelUpdateFunc(model);

            await _repository.SaveAsync(model).AnyContext();
            await AfterUpdateAsync(model).AnyContext();

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(model);

            return Ok(await MapAsync<TViewModel>(model, true).AnyContext());
        }

        protected async Task<IHttpActionResult> UpdateModelsAsync(string[] ids, Func<TModel, TModel> modelUpdateFunc) {
            var models = await GetModelsAsync(ids).AnyContext();
            if (models == null || models.Count == 0)
                return NotFound();

            if (modelUpdateFunc != null)
                models.ForEach(m => modelUpdateFunc(m));

            await _repository.SaveAsync(models).AnyContext();
            models.ForEach(async m => await AfterUpdateAsync(m).AnyContext());

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(models);

            return Ok(await MapAsync<TViewModel>(models, true).AnyContext());
        }

        protected virtual string GetEntityLink(string id) {
            return Url.Link($"Get{typeof(TModel).Name}ById", new {
                id
            });
        }

        protected virtual string GetEntityResourceLink(string id, string type) {
            return GetResourceLink(Url.Link($"Get{typeof(TModel).Name}ById", new {
                id
            }), type);
        }

        protected virtual string GetEntityLink<TEntityType>(string id) {
            return Url.Link($"Get{typeof(TEntityType).Name}ById", new {
                id
            });
        }

        protected virtual string GetEntityResourceLink<TEntityType>(string id, string type) {
            return GetResourceLink(Url.Link($"Get{typeof(TEntityType).Name}ById", new {
                id
            }), type);
        }

        protected virtual Task<PermissionResult> CanAddAsync(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (_isOrganization || orgModel == null)
                return Task.FromResult(PermissionResult.Allow);

            if (!CanAccessOrganization(orgModel.OrganizationId))
                return Task.FromResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

            return Task.FromResult(PermissionResult.Allow);
        }

        protected virtual Task<TModel> AddModelAsync(TModel value) {
            return _repository.AddAsync(value);
        }

        protected virtual Task<TModel> AfterAddAsync(TModel value) {
            return Task.FromResult(value);
        }

        protected virtual Task<TModel> AfterUpdateAsync(TModel value) {
            return Task.FromResult(value);
        }

        public virtual async Task<IHttpActionResult> PatchAsync(string id, Delta<TUpdateModel> changes) {
            TModel original = await GetModelAsync(id, false).AnyContext();
            if (original == null)
                return NotFound();

            // if there are no changes in the delta, then ignore the request
            if (changes == null || !changes.GetChangedPropertyNames().Any())
                return await OkModelAsync(original).AnyContext();

            var permission = await CanUpdateAsync(original, changes).AnyContext();
            if (!permission.Allowed)
                return Permission(permission);

            try {
                await UpdateModelAsync(original, changes).AnyContext();
                await AfterPatchAsync(original).AnyContext();
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return await OkModelAsync(original).AnyContext();
        }

        protected virtual Task<PermissionResult> CanUpdateAsync(TModel original, Delta<TUpdateModel> changes) {
            var orgModel = original as IOwnedByOrganization;
            if (orgModel != null && !CanAccessOrganization(orgModel.OrganizationId))
                return Task.FromResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

            if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
                return Task.FromResult(PermissionResult.DenyWithMessage("OrganizationId cannot be modified."));

            return Task.FromResult(PermissionResult.Allow);
        }

        protected virtual Task<TModel> UpdateModelAsync(TModel original, Delta<TUpdateModel> changes) {
            changes.Patch(original);
            return _repository.SaveAsync(original);
        }

        protected virtual Task<TModel> AfterPatchAsync(TModel value) {
            return Task.FromResult(value);
        }

        public virtual async Task<IHttpActionResult> DeleteAsync(string[] ids) {
            var items = await GetModelsAsync(ids, false).AnyContext();
            if (!items.Any())
                return NotFound();

            var results = new ModelActionResults();
            results.AddNotFound(ids.Except(items.Select(i => i.Id)));

            foreach (var model in items.ToList()) {
                var permission = await CanDeleteAsync(model).AnyContext();
                if (permission.Allowed)
                    continue;

                items.Remove(model);
                results.Failure.Add(permission);
            }

            if (items.Count == 0)
                return results.Failure.Count == 1 ? Permission(results.Failure.First()) : BadRequest(results);

            try {
                await DeleteModelsAsync(items).AnyContext();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();
                return StatusCode(HttpStatusCode.InternalServerError);
            }

            if (results.Failure.Count == 0)
                return StatusCode(HttpStatusCode.NoContent);

            results.Success.AddRange(items.Select(i => i.Id));
            return BadRequest(results);
        }

        protected virtual Task<PermissionResult> CanDeleteAsync(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel != null && !CanAccessOrganization(orgModel.OrganizationId))
                return Task.FromResult(PermissionResult.DenyWithNotFound(value.Id));

            return Task.FromResult(PermissionResult.Allow);
        }

        protected virtual Task DeleteModelsAsync(ICollection<TModel> values) {
            return _repository.RemoveAsync(values);
        }

        protected override async Task CreateMapsAsync() {
            await base.CreateMapsAsync().AnyContext();

            if (Mapper.FindTypeMapFor<TNewModel, TModel>() == null)
                Mapper.CreateMap<TNewModel, TModel>();

            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();
        }
    }
}