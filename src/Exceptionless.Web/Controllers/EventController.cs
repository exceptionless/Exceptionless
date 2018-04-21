using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Controllers {
    [Route(API_PREFIX + "/events")]
    [Authorize(Policy = AuthorizationRoles.ClientPolicy)]
    public class EventController : RepositoryApiController<IEventRepository, PersistentEvent, PersistentEvent, PersistentEvent, UpdateEvent> {
        private static readonly HashSet<string> _ignoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_token", "api_key", "apikey" };

        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly EventPostService _eventPostService;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;
        private readonly IValidator<UserDescription> _userDescriptionValidator;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly ICacheClient _cache;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public EventController(IEventRepository repository,
            IOrganizationRepository organizationRepository,
            IProjectRepository projectRepository,
            IStackRepository stackRepository,
            EventPostService eventPostService,
            IQueue<EventUserDescription> eventUserDescriptionQueue,
            IValidator<UserDescription> userDescriptionValidator,
            FormattingPluginManager formattingPluginManager,
            ICacheClient cacheClient,
            JsonSerializerSettings jsonSerializerSettings,
            IMapper mapper,
            PersistentEventQueryValidator validator,
            ILoggerFactory loggerFactory) : base(repository, mapper, validator, loggerFactory) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventPostService = eventPostService;
            _eventUserDescriptionQueue = eventUserDescriptionQueue;
            _userDescriptionValidator = userDescriptionValidator;
            _formattingPluginManager = formattingPluginManager;
            _cache = cacheClient;
            _jsonSerializerSettings = jsonSerializerSettings;

            AllowedDateFields.Add(EventIndexType.Alias.Date);
            DefaultDateField = EventIndexType.Alias.Date;
        }

        /// <summary>
        /// Count
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="aggregations">A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("count")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<CountResult>))]
        public async Task<IActionResult> GetCountAsync([FromQuery] string filter = null, [FromQuery] string aggregations = null, [FromQuery] string time = null, [FromQuery] string offset = null) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.Count(o => !o.IsSuspended) == 0)
                return Ok(CountResult.Empty);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetCountImplAsync(sf, ti, filter, aggregations);
        }

        /// <summary>
        /// Count by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="aggregations">A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events/count")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<CountResult>))]
        public async Task<IActionResult> GetCountByOrganizationAsync(string organizationId, [FromQuery] string filter = null, [FromQuery] string aggregations = null, [FromQuery] string time = null, [FromQuery] string offset = null) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organization);
            return await GetCountImplAsync(sf, ti, filter, aggregations);
        }

        /// <summary>
        /// Count by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="aggregations">A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/count")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<CountResult>))]
        public async Task<IActionResult> GetCountByProjectAsync(string projectId, [FromQuery] string filter = null, [FromQuery] string aggregations = null, [FromQuery] string time = null, [FromQuery] string offset = null) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project));
            var sf = new ExceptionlessSystemFilter(project, organization);
            return await GetCountImplAsync(sf, ti, filter, aggregations);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the event.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="404">The event occurrence could not be found.</response>
        /// <response code="426">Unable to view event occurrence due to plan limits.</response>
        [HttpGet("{id:objectid}", Name = "GetPersistentEventById")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(PersistentEvent))]
        public async Task<IActionResult> GetByIdAsync(string id, [FromQuery] string filter = null, [FromQuery] string time = null, [FromQuery] string offset = null) {
            var model = await GetModelAsync(id, false);
            if (model == null)
                return NotFound();

            var organization = await GetOrganizationAsync(model.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended || organization.RetentionDays > 0 && model.Date.UtcDateTime < SystemClock.UtcNow.SubtractDays(organization.RetentionDays))
                return PlanLimitReached("Unable to view event occurrence due to plan limits.");

            if (!String.IsNullOrEmpty(filter))
                filter = filter.ReplaceFirst("stack:current", $"stack:{model.StackId}");

            var pr = await _validator.ValidateQueryAsync(filter);
            if (!pr.IsValid)
                return OkWithLinks(model, GetEntityResourceLink<Stack>(model.StackId, "parent"));

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organization);
            var result = await _repository.GetPreviousAndNextEventIdsAsync(model, sf, filter, ti.Range.UtcStart, ti.Range.UtcEnd);
            return OkWithLinks(model, new [] { GetEntityResourceLink(result.Previous, "previous"), GetEntityResourceLink(result.Next, "next"), GetEntityResourceLink<Stack>(model.StackId, "parent") });
        }

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetAsync([FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.Count(o => !o.IsSuspended) == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
        }

        private async Task<IActionResult> GetInternalAsync(ExceptionlessSystemFilter sf, TimeInfo ti, string filter = null, string sort = null, string mode = null, int page = 1, int limit = 10, bool usesPremiumFeatures = false) {
            page = GetPage(page);
            limit = GetLimit(limit);
            int skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(EmptyModels);

            var pr = await _validator.ValidateQueryAsync(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            sf.UsesPremiumFeatures = pr.UsesPremiumFeatures || usesPremiumFeatures;

            FindResults<PersistentEvent> events;
            try {
                events = await _repository.GetByFilterAsync(ShouldApplySystemFilter(sf, filter) ? sf : null, filter, sort, ti.Field, ti.Range.UtcStart, ti.Range.UtcEnd, o => o.PageNumber(page).PageLimit(limit));
            } catch (ApplicationException ex) {
                using (_logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Page = page, Limit = limit }).Tag("Search").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, "An error has occurred. Please check your search filter.");

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase))
                return OkWithResourceLinks(events.Documents.Select(e => {
                    var summaryData = _formattingPluginManager.GetEventSummaryData(e);
                    return new EventSummaryModel {
                        TemplateKey = summaryData.TemplateKey,
                        Id = e.Id,
                        Date = e.Date,
                        Data = summaryData.Data
                    };
                }).ToList(), events.HasMore && !NextPageExceedsSkipLimit(page, limit), page, events.Total);

            return OkWithResourceLinks(events.Documents, events.HasMore && !NextPageExceedsSkipLimit(page, limit), page, events.Total);
        }

        /// <summary>
        /// Get by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The organization could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetByOrganizationAsync(string organizationId = null, [FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
        }

        /// <summary>
        /// Get by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetByProjectAsync(string projectId, [FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project));
            var sf = new ExceptionlessSystemFilter(project, organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
        }

        /// <summary>
        /// Get by stack
        /// </summary>
        /// <param name="stackId">The identifier of the stack.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The stack could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/stacks/{stackId:objectid}/events")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetByStackAsync(string stackId, [FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var stack = await GetStackAsync(stackId);
            if (stack == null)
                return NotFound();

            var organization = await GetOrganizationAsync(stack.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(stack));
            var sf = new ExceptionlessSystemFilter(stack, organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
        }

        /// <summary>
        /// Get by reference id
        /// </summary>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("by-ref/{referenceId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetByReferenceIdAsync(string referenceId, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository);
            if (organizations.Count(o => !o.IsSuspended) == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(null, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, String.Concat("reference:", referenceId), null, mode, page, limit);
        }

        /// <summary>
        /// Get by reference id
        /// </summary>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetByReferenceIdAsync(string referenceId, string projectId, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(null, offset, organization.GetRetentionUtcCutoff(project));
            var sf = new ExceptionlessSystemFilter(project, organization);
            return await GetInternalAsync(sf, ti, String.Concat("reference:", referenceId), null,  mode, page, limit);
        }

        /// <summary>
        /// Get a list of all sessions or events by a session id
        /// </summary>
        /// <param name="sessionId">An identifier that represents a session of events.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("sessions/{sessionId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetBySessionIdAsync(string sessionId, [FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.Count(o => !o.IsSuspended) == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, $"(reference:{sessionId} OR ref.session:{sessionId}) {filter}", sort, mode, page, limit, true);
        }

        /// <summary>
        /// Get a list of by a session id
        /// </summary>
        /// <param name="sessionId">An identifier that represents a session of events.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/sessions/{sessionId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetBySessionIdAndProjectAsync(string sessionId, string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project));
            var sf = new ExceptionlessSystemFilter(project, organization);
            return await GetInternalAsync(sf, ti, $"(reference:{sessionId} OR ref.session:{sessionId}) {filter}", sort, mode, page, limit, true);
        }

        /// <summary>
        /// Get a list of all sessions
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("sessions")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetSessionsAsync([FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.Count(o => !o.IsSuspended) == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, $"type:{Event.KnownTypes.Session} {filter}", sort, mode, page, limit, true);
        }

        /// <summary>
        /// Get a list of all sessions
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events/sessions")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetSessionByOrganizationAsync(string organizationId, [FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilter(organization);
            return await GetInternalAsync(sf, ti, $"type:{Event.KnownTypes.Session} {filter}", sort, mode, page, limit, true);
        }

        /// <summary>
        /// Get a list of all sessions
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/sessions")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> GetSessionByProjectAsync(string projectId, [FromQuery] string filter = null, [FromQuery] string sort = null, [FromQuery] string time = null, [FromQuery] string offset = null, [FromQuery] string mode = null, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project));
            var sf = new ExceptionlessSystemFilter(project, organization);
            return await GetInternalAsync(sf, ti, $"type:{Event.KnownTypes.Session} {filter}", sort, mode, page, limit, true);
        }

        /// <summary>
        /// Set user description
        /// </summary>
        /// <remarks>You can also save an end users contact information and a description of the event. This is really useful for error events as a user can specify reproduction steps in the description.</remarks>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <param name="description">The user description.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <response code="400">Description must be specified.</response>
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpPost("by-ref/{referenceId:identifier}/user-description")]
        [HttpPost("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}/user-description")]
        [ConfigurationResponseFilter]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(List<PersistentEvent>))]
        public async Task<IActionResult> SetUserDescriptionAsync(string referenceId, [FromBody] UserDescription description, string projectId = null) {
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();

            if (description == null)
                return BadRequest("Description must be specified.");

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var result = await _userDescriptionValidator.ValidateAsync(description);
            if (!result.IsValid)
                return BadRequest(result.Errors.ToErrorMessage());

            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            // Set the project for the configuration response filter.
            Request.SetProject(project);

            var eventUserDescription = await MapAsync<EventUserDescription>(description);
            eventUserDescription.ProjectId = projectId;
            eventUserDescription.ReferenceId = referenceId;

            await _eventUserDescriptionQueue.EnqueueAsync(eventUserDescription);
            return StatusCode(StatusCodes.Status202Accepted);
        }

        [HttpPatch("~/api/v1/error/{id:objectid}")]
        [ConfigurationResponseFilter]
        public async Task<IActionResult> LegacyPatchAsync(string id, [FromBody] Delta<UpdateEvent> changes) {
            if (changes == null)
                return Ok();

            if (changes.UnknownProperties.TryGetValue("UserEmail", out var value))
                changes.TrySetPropertyValue("EmailAddress", value);
            if (changes.UnknownProperties.TryGetValue("UserDescription", out value))
                changes.TrySetPropertyValue("Description", value);

            var userDescription = new UserDescription();
            changes.Patch(userDescription);

            return await SetUserDescriptionAsync(id, userDescription);
        }

        /// <summary>
        /// Submit heartbeat
        /// </summary>
        /// <param name="id">The session id or user id.</param>
        /// <param name="close">If true, the session will be closed.</param>
        /// <response code="200">OK</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpGet("session/heartbeat")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> RecordHeartbeatAsync([FromQuery] string id = null, [FromQuery] bool close = false) {
            if (Settings.Current.EventSubmissionDisabled || String.IsNullOrEmpty(id))
                return Ok();

            string projectId = Request.GetDefaultProjectId();
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            string identityHash = id.ToSHA1();
            string heartbeatCacheKey = String.Concat("Project:", projectId, ":heartbeat:", identityHash);
            try {
                await Task.WhenAll(
                    _cache.SetAsync(heartbeatCacheKey, SystemClock.UtcNow, TimeSpan.FromHours(2)),
                    close ? _cache.SetAsync(String.Concat(heartbeatCacheKey, "-close"), true, TimeSpan.FromHours(2)) : Task.CompletedTask
                );
            } catch (Exception ex) {
                if (projectId != Settings.Current.InternalProjectId) {
                    using (_logger.BeginScope(new ExceptionlessState().Project(projectId).Identity(CurrentUser?.EmailAddress).Property("User", CurrentUser).Property("Id", id).Property("Close", close).SetHttpContext(HttpContext)))
                        _logger.LogError(ex, "Error enqueuing session heartbeat.");
                }

                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <remarks>
        /// You can create an event using query string parameters.
        ///
        /// Feature usage named build with a duration of 10:
        /// <code><![CDATA[/events/submit?access_token=YOUR_API_KEY&type=usage&source=build&value=10]]></code>
        /// OR
        /// <code><![CDATA[/events/submit/usage?access_token=YOUR_API_KEY&source=build&value=10]]></code>
        ///
        /// Log with message, geo and extended data
        /// <code><![CDATA[/events/submit?access_token=YOUR_API_KEY&type=log&message=Hello World&source=server01&geo=32.85,-96.9613&randomproperty=true]]></code>
        /// OR
        /// <code><![CDATA[/events/submit/log?access_token=YOUR_API_KEY&message=Hello World&source=server01&geo=32.85,-96.9613&randomproperty=true]]></code>
        /// </remarks>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="apiVersion">The api version that should be used</param>
        /// <param name="type">The event type</param>
        /// <param name="userAgent">The user agent that submitted the event.</param>
        /// <param name="parameters">Query String parameters that control what properties are set on the event</param>
        /// <response code="200">OK</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpGet("~/api/v{apiVersion:int=2}/events/submit")]
        [HttpGet("~/api/v{apiVersion:int=2}/events/submit/{type:minlength(1)}")]
        [HttpGet("~/api/v{apiVersion:int=2}/projects/{projectId:objectid}/events/submit")]
        [HttpGet("~/api/v{apiVersion:int=2}/projects/{projectId:objectid}/events/submit/{type:minlength(1)}")]
        [ConfigurationResponseFilter]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSubmitEventAsync(string projectId = null, int apiVersion = 2, string type = null, [UserAgent] string userAgent = null, [QueryStringParameters] IDictionary<string, string[]> parameters = null) {
            var filteredParameters = parameters?.Where(p => !String.IsNullOrEmpty(p.Key) && !p.Value.All(String.IsNullOrEmpty) && !_ignoredKeys.Contains(p.Key)).ToList();
            if (filteredParameters == null || filteredParameters.Count == 0)
                return Ok();

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = Request.GetProject();
            if (!String.Equals(project?.Id, projectId)) {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Project {RequestProjectId} from request doesn't match project route id {RouteProjectId}", project?.Id, projectId);

                project = await GetProjectAsync(projectId);

                // Set the project for the configuration response filter.
                Request.SetProject(project);
            }

            if (project == null)
                return NotFound();

            string contentEncoding = Request.Headers.TryGetAndReturn(Headers.ContentEncoding);
            var ev = new Event { Type = !String.IsNullOrEmpty(type) ? type : Event.KnownTypes.Log };

            string identity = null;
            string identityName = null;

            var exclusions = project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions).ToList();
            foreach (var kvp in filteredParameters) {
                switch (kvp.Key.ToLowerInvariant()) {
                    case "type":
                        ev.Type = kvp.Value.FirstOrDefault();
                        break;
                    case "source":
                        ev.Source = kvp.Value.FirstOrDefault();
                        break;
                    case "message":
                        ev.Message = kvp.Value.FirstOrDefault();
                        break;
                    case "reference":
                        ev.ReferenceId = kvp.Value.FirstOrDefault();
                        break;
                    case "date":
                        if (DateTimeOffset.TryParse(kvp.Value.FirstOrDefault(), out var dtValue))
                            ev.Date = dtValue;
                        break;
                    case "count":
                        if (Int32.TryParse(kvp.Value.FirstOrDefault(), out var intValue))
                            ev.Count = intValue;
                        break;
                    case "value":
                        if (Decimal.TryParse(kvp.Value.FirstOrDefault(), out var decValue))
                            ev.Value = decValue;
                        break;
                    case "geo":
                        if (GeoResult.TryParse(kvp.Value.FirstOrDefault(), out var geo))
                            ev.Geo = geo.ToString();
                        break;
                    case "tags":
                        ev.Tags.AddRange(kvp.Value.SelectMany(t => t.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)).Distinct());
                        break;
                    case "identity":
                        identity = kvp.Value.FirstOrDefault();
                        break;
                    case "identity.name":
                        identityName = kvp.Value.FirstOrDefault();
                        break;
                    default:
                        if (kvp.Key.AnyWildcardMatches(exclusions, true))
                            continue;

                        if (kvp.Value.Length > 1)
                            ev.Data[kvp.Key] = kvp.Value;
                        else
                            ev.Data[kvp.Key] = kvp.Value.FirstOrDefault();

                        break;
                }
            }

            ev.SetUserIdentity(identity, identityName);

            try {
                string mediaType = String.Empty;
                string charSet = String.Empty;
                if (Request.ContentType != null && MediaTypeHeaderValue.TryParse(Request.ContentType, out var contentTypeHeader)) {
                    mediaType = contentTypeHeader.MediaType.ToString();
                    charSet = contentTypeHeader.Charset.ToString();
                }

                var stream = new MemoryStream(ev.GetBytes(_jsonSerializerSettings));
                await _eventPostService.EnqueueAsync(new EventPost {
                    ApiVersion = apiVersion,
                    CharSet = charSet,
                    ContentEncoding = contentEncoding,
                    IpAddress = Request.GetClientIpAddress(),
                    MediaType = mediaType,
                    OrganizationId = project.OrganizationId,
                    ProjectId = project.Id,
                    UserAgent = userAgent
                }, stream);
            } catch (Exception ex) {
                if (projectId != Settings.Current.InternalProjectId) {
                    using (_logger.BeginScope(new ExceptionlessState().Project(projectId).Identity(CurrentUser?.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                        _logger.LogError(ex, "Error enqueuing event post.");
                }

                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }


        [HttpPost("~/api/v1/error")]
        [ConfigurationResponseFilter]
        public Task<IActionResult> LegacyPostAsync([UserAgent] string userAgent = null) {
            return PostAsync(null, 1, userAgent);
        }

        ///  <summary>
        ///  Create
        ///  </summary>
        ///  <remarks>
        ///  You can create an event by posting any uncompressed or compressed (gzip or deflate) string or json object. If we know how to handle it
        ///  we will create a new event. If none of the JSON properties match the event object then we will create a new event and place your JSON
        ///  object into the events data collection.
        /// 
        ///  You can also post a multi-line string. We automatically split strings by the \n character and create a new log event for every line.
        /// 
        ///  Simple event:
        ///  <code>
        ///      { "message": "Exceptionless is amazing!" }
        ///  </code>
        ///  
        ///  Simple log event with user identity:
        ///  <code>
        ///      {
        ///          "type": "log",
        ///          "message": "Exceptionless is amazing!",
        ///          "date":"2020-01-01T12:00:00.0000000-05:00",
        ///          "@user":{ "identity":"123456789", "name": "Test User" }
        ///      }
        ///  </code>
        /// 
        ///  Multiple events from string content:
        ///  <code>
        ///      Exceptionless is amazing!
        ///      Exceptionless is really amazing!
        ///  </code>
        /// 
        ///  Simple error:
        ///  <code>
        ///      {
        ///          "type": "error",
        ///          "date":"2020-01-01T12:00:00.0000000-05:00",
        ///          "@simple_error": {
        ///              "message": "Simple Exception",
        ///              "type": "System.Exception",
        ///              "stack_trace": "   at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests.cs:line 77"
        ///          }
        ///      }
        ///  </code>
        ///  </remarks>
        /// <param name="projectId">The identifier of the project.</param>
        ///  <param name="apiVersion">The api version that should be used</param>
        ///  <param name="userAgent">The user agent that submitted the event.</param>
        ///  <response code="202">Accepted</response>
        ///  <response code="400">No project id specified and no default project was found.</response>
        ///  <response code="404">No project was found.</response>
        [HttpPost("~/api/v{apiVersion:int=2}/events")]
        [HttpPost("~/api/v{apiVersion:int=2}/projects/{projectId:objectid}/events")]
        [ConfigurationResponseFilter]
        [SwaggerResponse(StatusCodes.Status202Accepted)]
        public async Task <IActionResult> PostAsync(string projectId = null, int apiVersion = 2, [UserAgent]string userAgent = null) {
            if (Request.ContentLength.HasValue && Request.ContentLength.Value <= 0)
                return StatusCode(StatusCodes.Status202Accepted);

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = Request.GetProject();
            if (!String.Equals(project?.Id, projectId)) {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Project {RequestProjectId} from request doesn't match project route id {RouteProjectId}", project?.Id, projectId);

                project = await GetProjectAsync(projectId);

                // Set the project for the configuration response filter.
                Request.SetProject(project);
            }

            if (project == null)
                return NotFound();

            try {
                string mediaType = String.Empty;
                string charSet = String.Empty;
                if (Request.ContentType != null) {
                    var contentType = MediaTypeHeaderValue.Parse(Request.ContentType);
                    mediaType = contentType.MediaType.ToString();
                    charSet = contentType.Charset.ToString();
                }

                await _eventPostService.EnqueueAsync(new EventPost {
                    ApiVersion = apiVersion,
                    CharSet = charSet,
                    ContentEncoding = Request.Headers.TryGetAndReturn(Headers.ContentEncoding),
                    IpAddress = Request.GetClientIpAddress(),
                    MediaType = mediaType,
                    OrganizationId = project.OrganizationId,
                    ProjectId = project.Id,
                    UserAgent = userAgent,
                }, Request.Body);
            } catch (Exception ex) {
                if (projectId != Settings.Current.InternalProjectId) {
                    using (_logger.BeginScope(new ExceptionlessState().Project(projectId).Identity(CurrentUser?.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                        _logger.LogError(ex, "Error enqueuing event post.");
                }

                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return StatusCode(StatusCodes.Status202Accepted);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of event identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more event occurrences were not found.</response>
        /// <response code="500">An error occurred while deleting one or more event occurrences.</response>
        [HttpDelete("{ids:objectids}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public Task<IActionResult> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        private Task<Organization> GetOrganizationAsync(string organizationId, bool useCache = true) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return null;

            return _organizationRepository.GetByIdAsync(organizationId, o => o.Cache(useCache));
        }

        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return null;

            return project;
        }

        private async Task<Stack> GetStackAsync(string stackId, bool useCache = true) {
            if (String.IsNullOrEmpty(stackId))
                return null;

            var stack = await _stackRepository.GetByIdAsync(stackId, o => o.Cache(useCache));
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return null;

            return stack;
        }
    }
}
