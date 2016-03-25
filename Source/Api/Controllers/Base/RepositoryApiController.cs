using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Logging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    public abstract class RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel> : ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> where TRepository : IRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() where TNewModel : class, new() where TUpdateModel : class, new() {
        protected readonly ILogger _logger;

        public RepositoryApiController(TRepository repository, ILoggerFactory loggerFactory, IMapper mapper) : base(repository, mapper) {
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public virtual async Task<IHttpActionResult> PostAsync(TNewModel value) {
            if (value == null)
                return BadRequest();

            TModel mapped = await MapAsync<TModel>(value);

            var orgModel = mapped as IOwnedByOrganization;
            // if no organization id is specified, default to the user's 1st associated org.
            if (!_isOrganization && orgModel != null && String.IsNullOrEmpty(orgModel.OrganizationId) && GetAssociatedOrganizationIds().Any())
                orgModel.OrganizationId = Request.GetDefaultOrganizationId();

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

        protected async Task<IHttpActionResult> UpdateModelAsync(string id, Func<TModel, Task<TModel>> modelUpdateFunc) {
            TModel model = await GetModelAsync(id);
            if (model == null)
                return NotFound();

            if (modelUpdateFunc != null)
                model = await modelUpdateFunc(model);

            await _repository.SaveAsync(model, true);
            await AfterUpdateAsync(model);

            if (typeof(TViewModel) == typeof(TModel))
                return Ok(model);

            return Ok(await MapAsync<TViewModel>(model, true));
        }

        protected async Task<IHttpActionResult> UpdateModelsAsync(string[] ids, Func<TModel, Task<TModel>> modelUpdateFunc) {
            var models = await GetModelsAsync(ids, false);
            if (models == null || models.Count == 0)
                return NotFound();

            if (modelUpdateFunc != null)
                foreach (var model in models)
                    await modelUpdateFunc(model);

            await _repository.SaveAsync(models, true);
            foreach (var model in models)
                await AfterUpdateAsync(model);

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

            if (!CanAccessOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            return PermissionResult.Allow;
        }

        protected virtual Task<TModel> AddModelAsync(TModel value) {
            return _repository.AddAsync(value, true);
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
            if (orgModel != null && !CanAccessOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
                return PermissionResult.DenyWithMessage("OrganizationId cannot be modified.");

            return PermissionResult.Allow;
        }

        protected virtual Task<TModel> UpdateModelAsync(TModel original, Delta<TUpdateModel> changes) {
            changes.Patch(original);
            return _repository.SaveAsync(original, true);
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

            IEnumerable<string> workIds;
            try {
                workIds = await DeleteModelsAsync(items) ?? new List<string>();
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();
                return StatusCode(HttpStatusCode.InternalServerError);
            }

            if (results.Failure.Count == 0)
                return WorkInProgress(workIds);

            results.Workers.AddRange(workIds);
            results.Success.AddRange(items.Select(i => i.Id));
            return BadRequest(results);
        }

        protected virtual async Task<PermissionResult> CanDeleteAsync(TModel value) {
            var orgModel = value as IOwnedByOrganization;
            if (orgModel != null && !CanAccessOrganization(orgModel.OrganizationId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return PermissionResult.Allow;
        }

        protected virtual async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<TModel> values) {
            await _repository.RemoveAsync(values);
            return new List<string>();
        }
    }
}
