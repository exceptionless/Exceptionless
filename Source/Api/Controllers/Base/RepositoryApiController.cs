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
            if (!_isOrganization && orgModel != null && String.IsNullOrEmpty(orgModel.OrganizationId) && (await GetAssociatedOrganizationIdsAsync()).Any())
                orgModel.OrganizationId = await GetDefaultOrganizationIdAsync();

            TModel mapped = await MapAsync<TModel>(value);
            var permission = await CanAddAsync(mapped);
            if (!permission.Allowed)
                return Permission(permission);

            TModel model;
            try {
                model = await AddModelAsync(mapped);
                await AfterAddAsync(model);
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return Created(new Uri(GetEntityLink(model.Id)), await MapAsync<TViewModel>(model, true));
        }

        protected async Task<IHttpActionResult> UpdateModelAsync(string id, Func<TModel, TModel> modelUpdateFunc) {
            TModel model = await GetModelAsync(id);
            if (model == null)
                return NotFound();

            if (modelUpdateFunc != null)
                model = modelUpdateFunc(model);

            await _repository.SaveAsync(model);
            await AfterUpdateAsync(model);

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(model);

            return Ok(await MapAsync<TViewModel>(model, true));
        }

        protected async Task<IHttpActionResult> UpdateModelsAsync(string[] ids, Func<TModel, TModel> modelUpdateFunc) {
            var models = await GetModelsAsync(ids);
            if (models == null || models.Count == 0)
                return NotFound();

            if (modelUpdateFunc != null)
                models.ForEach(m => modelUpdateFunc(m));

            await _repository.SaveAsync(models);
            models.ForEach(async m => await AfterUpdateAsync(m));

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(models);

            return Ok(await MapAsync<TViewModel>(models, true));
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

        protected virtual async Task<PermissionResult> CanAddAsync(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (_isOrganization || orgModel == null)
                return PermissionResult.Allow;

            if (!await CanAccessOrganizationAsync(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            return PermissionResult.Allow;
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
            TModel original = await GetModelAsync(id, false);
            if (original == null)
                return NotFound();

            // if there are no changes in the delta, then ignore the request
            if (changes == null || !changes.GetChangedPropertyNames().Any())
                return await OkModelAsync(original);

            var permission = await CanUpdateAsync(original, changes);
            if (!permission.Allowed)
                return Permission(permission);

            try {
                await UpdateModelAsync(original, changes);
                await AfterPatchAsync(original);
            } catch (ValidationException ex) {
                return BadRequest(ex.Errors.ToErrorMessage());
            }

            return await OkModelAsync(original);
        }

        protected virtual async Task<PermissionResult> CanUpdateAsync(TModel original, Delta<TUpdateModel> changes) {
            var orgModel = original as IOwnedByOrganization;
            if (orgModel != null && !await CanAccessOrganizationAsync(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
                return PermissionResult.DenyWithMessage("OrganizationId cannot be modified.");

            return PermissionResult.Allow;
        }

        protected virtual Task<TModel> UpdateModelAsync(TModel original, Delta<TUpdateModel> changes) {
            changes.Patch(original);
            return _repository.SaveAsync(original);
        }

        protected virtual Task<TModel> AfterPatchAsync(TModel value) {
            return Task.FromResult(value);
        }

        public virtual async Task<IHttpActionResult> DeleteAsync(string[] ids) {
            var items = await GetModelsAsync(ids, false);
            if (!items.Any())
                return NotFound();

            var results = new ModelActionResults();
            results.AddNotFound(ids.Except(items.Select(i => i.Id)));

            foreach (var model in items.ToList()) {
                var permission = await CanDeleteAsync(model);
                if (permission.Allowed)
                    continue;

                items.Remove(model);
                results.Failure.Add(permission);
            }

            if (items.Count == 0)
                return results.Failure.Count == 1 ? Permission(results.Failure.First()) : BadRequest(results);

            try {
                await DeleteModelsAsync(items);
            } catch (Exception ex) {
                var loggedInUser = await GetExceptionlessUserAsync();
                Log.Error().Exception(ex).Identity(loggedInUser.EmailAddress).Property("User", loggedInUser).ContextProperty("HttpActionContext", ActionContext).Write();
                return StatusCode(HttpStatusCode.InternalServerError);
            }

            if (results.Failure.Count == 0)
                return StatusCode(HttpStatusCode.NoContent);

            results.Success.AddRange(items.Select(i => i.Id));
            return BadRequest(results);
        }

        protected virtual async Task<PermissionResult> CanDeleteAsync(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel != null && !await CanAccessOrganizationAsync(orgModel.OrganizationId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return PermissionResult.Allow;
        }

        protected virtual Task DeleteModelsAsync(ICollection<TModel> values) {
            return _repository.RemoveAsync(values);
        }

        protected override async Task CreateMapsAsync() {
            await base.CreateMapsAsync();

            if (Mapper.FindTypeMapFor<TNewModel, TModel>() == null)
                Mapper.CreateMap<TNewModel, TModel>();

            if (Mapper.FindTypeMapFor<TModel, TViewModel>() == null)
                Mapper.CreateMap<TModel, TViewModel>();
        }
    }
}