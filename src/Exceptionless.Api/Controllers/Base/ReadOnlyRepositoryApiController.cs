using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Controllers {
    public abstract class ReadOnlyRepositoryApiController<TRepository, TModel, TViewModel> : ExceptionlessApiController where TRepository : ISearchableReadOnlyRepository<TModel> where TModel : class, IIdentity, new() where TViewModel : class, IIdentity, new() {
        protected readonly TRepository _repository;
        protected static readonly bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(TModel));
        protected static readonly bool _isOrganization = typeof(TModel) == typeof(Organization);
        protected static readonly bool _supportsSoftDeletes = typeof(ISupportSoftDeletes).IsAssignableFrom(typeof(TModel));
        protected static readonly IReadOnlyCollection<TModel> EmptyModels = new List<TModel>(0).AsReadOnly();
        protected readonly IMapper _mapper;
        protected readonly IQueryValidator _validator;
        protected readonly ILogger _logger;

        public ReadOnlyRepositoryApiController(TRepository repository, IMapper mapper, IQueryValidator validator, ILoggerFactory loggerFactory) {
            _repository = repository;
            _mapper = mapper;
            _validator = validator;
            _logger = loggerFactory.CreateLogger(GetType());
        }

        protected async Task<IActionResult> GetByIdImplAsync(string id) {
            var model = await GetModelAsync(id);
            if (model == null)
                return NotFound();

            return await OkModelAsync(model);
        }

        protected async Task<IActionResult> GetCountImplAsync(ExceptionlessSystemFilter sf, TimeInfo ti, string filter = null, string aggregations = null) {
            var pr = await _validator.ValidateQueryAsync(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            var far = await _validator.ValidateAggregationsAsync(aggregations);
            if (!far.IsValid)
                return BadRequest(far.Message);

            sf.UsesPremiumFeatures = pr.UsesPremiumFeatures || far.UsesPremiumFeatures;
            var query = new RepositoryQuery<TModel>()
                .SystemFilter(ShouldApplySystemFilter(sf, filter) ? sf : null)
                .DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field)
                .Index(ti.Range.UtcStart, ti.Range.UtcEnd);

            CountResult result;
            try {
                result = await _repository.CountBySearchAsync(query, filter, aggregations);
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Aggregations = aggregations }).Tag("Search").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, "An error has occurred. Please check your filter or aggregations.");

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }

        protected async Task<IActionResult> OkModelAsync(TModel model) {
            return Ok(await MapAsync<TViewModel>(model, true));
        }

        protected virtual async Task<TModel> GetModelAsync(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            var model = await _repository.GetByIdAsync(id, o => o.Cache(useCache));
            if (model == null)
                return null;

            if (_supportsSoftDeletes && ((ISupportSoftDeletes)model).IsDeleted)
                return null;

            if (_isOwnedByOrganization && !CanAccessOrganization(((IOwnedByOrganization)model).OrganizationId))
                return null;

            return model;
        }

        protected virtual async Task<IReadOnlyCollection<TModel>> GetModelsAsync(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return EmptyModels;

            var models = await _repository.GetByIdsAsync(ids, o => o.Cache(useCache));
            if (_supportsSoftDeletes)
                models = models.Where(m => !((ISupportSoftDeletes)m).IsDeleted).ToList();

            if (_isOwnedByOrganization)
                models = models.Where(m => CanAccessOrganization(((IOwnedByOrganization)m).OrganizationId)).ToList();

            return models;
        }

        #region Mapping

        protected async Task<TDestination> MapAsync<TDestination>(object source, bool isResult = false) {
            var destination = _mapper.Map<TDestination>(source);
            if (isResult)
                await AfterResultMapAsync(new List<TDestination>(new[] { destination }));

            return destination;
        }

        protected async Task<ICollection<TDestination>> MapCollectionAsync<TDestination>(object source, bool isResult = false) {
            var destination = _mapper.Map<ICollection<TDestination>>(source);
            if (isResult)
                await AfterResultMapAsync<TDestination>(destination);

            return destination;
        }

        protected virtual Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models) {
            foreach (var model in models.OfType<IData>())
                model.Data.RemoveSensitiveData();

            return Task.CompletedTask;
        }

        #endregion
    }
}
