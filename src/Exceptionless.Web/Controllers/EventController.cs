using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Web.Extensions;
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
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
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
using System.Text;

namespace Exceptionless.Web.Controllers {
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
        private readonly AppOptions _appOptions;

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
            AppOptions appOptions,
            ILoggerFactory loggerFactory
        ) : base(repository, mapper, validator, loggerFactory) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventPostService = eventPostService;
            _eventUserDescriptionQueue = eventUserDescriptionQueue;
            _userDescriptionValidator = userDescriptionValidator;
            _formattingPluginManager = formattingPluginManager;
            _cache = cacheClient;
            _jsonSerializerSettings = jsonSerializerSettings;
            _appOptions = appOptions;

            AllowedDateFields.Add(EventIndex.Alias.Date);
            DefaultDateField = EventIndex.Alias.Date;
        }

        /// <summary>
        /// Count
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="aggregations">A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("count")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<CountResult>> GetCountAsync(string filter = null, string aggregations = null, string time = null, string offset = null, string mode = null) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.All(o => o.IsSuspended))
                return Ok(CountResult.Empty);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
            return await CountInternalAsync(sf, ti, filter, aggregations, mode);
        }

        /// <summary>
        /// Count by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="aggregations">A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events/count")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<CountResult>> GetCountByOrganizationAsync(string organizationId, string filter = null, string aggregations = null, string time = null, string offset = null, string mode = null) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organization);
            return await CountInternalAsync(sf, ti, filter, aggregations, mode);
        }

        /// <summary>
        /// Count by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="aggregations">A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/count")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<CountResult>> GetCountByProjectAsync(string projectId, string filter = null, string aggregations = null, string time = null, string offset = null, string mode = null) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project, _appOptions.MaximumRetentionDays));
            var sf = new AppFilter(project, organization);
            return await CountInternalAsync(sf, ti, filter, aggregations, mode);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the event.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="404">The event occurrence could not be found.</response>
        /// <response code="426">Unable to view event occurrence due to plan limits.</response>
        [HttpGet("{id:objectid}", Name = "GetPersistentEventById")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<PersistentEvent>> GetAsync(string id, string time = null, string offset = null) {
            var model = await GetModelAsync(id, false);
            if (model == null)
                return NotFound();

            var organization = await GetOrganizationAsync(model.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended || organization.RetentionDays > 0 && model.Date.UtcDateTime < SystemClock.UtcNow.SubtractDays(organization.RetentionDays))
                return PlanLimitReached("Unable to view event occurrence due to plan limits.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organization);
            var result = await _repository.GetPreviousAndNextEventIdsAsync(model, sf, ti.Range.UtcStart, ti.Range.UtcEnd);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetAsync(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.All(o => o.IsSuspended))
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit, after);
        }

        private async Task<ActionResult<CountResult>> CountInternalAsync(AppFilter sf, TimeInfo ti, string filter = null, string aggregations = null, string mode = null) {
            var pr = await _validator.ValidateQueryAsync(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            var far = await _validator.ValidateAggregationsAsync(aggregations);
            if (!far.IsValid)
                return BadRequest(far.Message);

            sf.UsesPremiumFeatures = pr.UsesPremiumFeatures || far.UsesPremiumFeatures;

            if (mode == "stack_new")
                filter = AddFirstOccurrenceFilter(ti.Range, filter);

            var query = new RepositoryQuery<PersistentEvent>()
                .AppFilter(ShouldApplySystemFilter(sf, filter) ? sf : null)
                .DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field)
                .Index(ti.Range.UtcStart, ti.Range.UtcEnd);

            CountResult result;
            try {
                result = await _repository.CountAsync(q => q.SystemFilter(query).FilterExpression(filter).EnforceEventStackFilter().AggregationsExpression(aggregations));
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Aggregations = aggregations }).Tag("Search").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, "An error has occurred. Please check your filter or aggregations.");

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }

        private async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetInternalAsync(AppFilter sf, TimeInfo ti, string filter = null, string sort = null, string mode = null, int page = 1, int limit = 10, string after = null, bool usesPremiumFeatures = false) {
            page = GetPage(page);
            limit = GetLimit(limit);
            int skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(EmptyModels);

            var pr = await _validator.ValidateQueryAsync(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            sf.UsesPremiumFeatures = pr.UsesPremiumFeatures || usesPremiumFeatures;
            bool useSearchAfter = !String.IsNullOrEmpty(after);

            try {
                FindResults<PersistentEvent> events;
                switch (mode) {
                    case "summary":
                        events = await GetEventsInternalAsync(sf, ti, filter, sort, page, limit, after, useSearchAfter);
                        return OkWithResourceLinks(events.Documents.Select(e => {
                            var summaryData = _formattingPluginManager.GetEventSummaryData(e);
                            return new EventSummaryModel {
                                TemplateKey = summaryData.TemplateKey,
                                Id = e.Id,
                                Date = e.Date,
                                Data = summaryData.Data
                            };
                        }).ToList(), events.HasMore && !NextPageExceedsSkipLimit(page, limit), page, events.Total);
                    case "stack_recent":
                    case "stack_frequent":
                    case "stack_new":
                    case "stack_users":
                        if (!String.IsNullOrEmpty(sort))
                            return BadRequest("Sort is not supported in stack mode.");

                        var systemFilter = new RepositoryQuery<PersistentEvent>()
                            .AppFilter(ShouldApplySystemFilter(sf, filter) ? sf : null)
                            .EnforceEventStackFilter()
                            .DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, (PersistentEvent e) => e.Date)
                            .Index(ti.Range.UtcStart, ti.Range.UtcEnd);

                        string stackAggregations = mode switch {
                            "stack_recent" => "cardinality:user sum:count~1 min:date -max:date",
                            "stack_frequent" => "cardinality:user -sum:count~1 min:date max:date",
                            "stack_new" => "cardinality:user sum:count~1 -min:date max:date",
                            "stack_users" => "-cardinality:user sum:count~1 min:date max:date",
                            _ => null
                        };

                        if (mode == "stack_new")
                            filter = AddFirstOccurrenceFilter(ti.Range, filter);

                        var countResponse = await _repository.CountAsync(q => q
                            .SystemFilter(systemFilter)
                            .FilterExpression(filter)
                            .EnforceEventStackFilter()
                            .AggregationsExpression($"terms:(stack_id~{GetSkip(page + 1, limit) + 1} {stackAggregations})"));

                        var stackTerms = countResponse.Aggregations.Terms<string>("terms_stack_id");
                        if (stackTerms == null || stackTerms.Buckets.Count == 0)
                            return Ok(EmptyModels);

                        string[] stackIds = stackTerms.Buckets.Skip(skip).Take(limit + 1).Select(t => t.Key).ToArray();
                        var stacks = (await _stackRepository.GetByIdsAsync(stackIds)).Select(s => s.ApplyOffset(ti.Offset)).ToList();

                        var summaries = await GetStackSummariesAsync(stacks, stackTerms.Buckets, sf, ti);
                        return OkWithResourceLinks(summaries.Take(limit).ToList(), summaries.Count > limit, page);
                    default:
                        events = await GetEventsInternalAsync(sf, ti, filter, sort, page, limit, after, useSearchAfter);
                        return OkWithResourceLinks(events.Documents, events.HasMore && !NextPageExceedsSkipLimit(page, limit), page, events.Total);
                }
            } catch (ApplicationException ex) {
                string message = "An error has occurred. Please check your search filter.";
                if (ex is DocumentLimitExceededException)
                    message = $"An error has occurred. {ex.Message ?? "Please limit your search criteria."}";

                using (_logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Page = page, Limit = limit }).Tag("Search").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, message);

                return BadRequest(message);
            }
        }

        private string AddFirstOccurrenceFilter(DateTimeRange timeRange, string filter) {
            bool inverted = false;
            if (filter != null && filter.StartsWith("@!")) {
                inverted = true;
                filter = filter.Substring(2);
            }

            var sb = new StringBuilder();
            if (inverted)
                sb.Append("@!");

            sb.Append("first_occurrence:[");
            sb.Append((long)timeRange.UtcStart.Subtract(DateTime.UnixEpoch).TotalMilliseconds);
            sb.Append(" TO ");
            sb.Append((long)timeRange.UtcEnd.Subtract(DateTime.UnixEpoch).TotalMilliseconds);
            sb.Append(']');

            if (String.IsNullOrEmpty(filter))
                return sb.ToString();

            sb.Append(' ');

            bool isGrouped = filter.StartsWith('(') && filter.EndsWith(')');

            if (isGrouped)
                sb.Append(filter);
            else
                sb.Append('(').Append(filter).Append(')');

            return sb.ToString();
        }

        private Task<FindResults<PersistentEvent>> GetEventsInternalAsync(AppFilter sf, TimeInfo ti, string filter, string sort, int page, int limit, string after, bool useSearchAfter) {
            if (String.IsNullOrEmpty(sort))
                sort = "-date";

            return _repository.FindAsync(q => q.AppFilter(ShouldApplySystemFilter(sf, filter) ? sf : null).FilterExpression(filter).EnforceEventStackFilter().SortExpression(sort).DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field),
                o => useSearchAfter
                    ? o.SearchAfterPaging().SearchAfter(after).PageLimit(limit)
                    : o.PageNumber(page).PageLimit(limit));
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The organization could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetByOrganizationAsync(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit, after);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetByProjectAsync(string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project, _appOptions.MaximumRetentionDays));
            var sf = new AppFilter(project, organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit, after);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The stack could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/stacks/{stackId:objectid}/events")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetByStackAsync(string stackId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var stack = await GetStackAsync(stackId);
            if (stack == null)
                return NotFound();

            var organization = await GetOrganizationAsync(stack.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(stack, _appOptions.MaximumRetentionDays));
            var sf = new AppFilter(stack, organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit, after);
        }

        /// <summary>
        /// Get by reference id
        /// </summary>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("by-ref/{referenceId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetByReferenceIdAsync(string referenceId, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository);
            if (organizations.All(o => o.IsSuspended))
                return Ok(EmptyModels);

            var ti = GetTimeInfo(null, offset, organizations.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, String.Concat("reference:", referenceId), null, mode, page, limit, after);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetByReferenceIdAsync(string referenceId, string projectId, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(null, offset, organization.GetRetentionUtcCutoff(project, _appOptions.MaximumRetentionDays));
            var sf = new AppFilter(project, organization);
            return await GetInternalAsync(sf, ti, String.Concat("reference:", referenceId), null,  mode, page, limit, after);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("sessions/{sessionId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetBySessionIdAsync(string sessionId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.All(o => o.IsSuspended))
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, $"(reference:{sessionId} OR ref.session:{sessionId}) {filter}", sort, mode, page, limit, after, true);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/sessions/{sessionId:identifier}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetBySessionIdAndProjectAsync(string sessionId, string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project, _appOptions.MaximumRetentionDays));
            var sf = new AppFilter(project, organization);
            return await GetInternalAsync(sf, ti, $"(reference:{sessionId} OR ref.session:{sessionId}) {filter}", sort, mode, page, limit, after, true);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet("sessions")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetSessionsAsync(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
            if (organizations.All(o => o.IsSuspended))
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, $"type:{Event.KnownTypes.Session} {filter}", sort, mode, page, limit, after, true);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events/sessions")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetSessionByOrganizationAsync(string organizationId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays));
            var sf = new AppFilter(organization);
            return await GetInternalAsync(sf, ti, $"type:{Event.KnownTypes.Session} {filter}", sort, mode, page, limit, after, true);
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
        /// <param name="after">The after parameter is a cursor used for pagination and defines your place in the list of results. Pass in the last event id in the previous call to fetch the next page of the list.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view event occurrences for the suspended organization.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/sessions")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<ActionResult<IReadOnlyCollection<PersistentEvent>>> GetSessionByProjectAsync(string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, string after = null) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view event occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project, _appOptions.MaximumRetentionDays));
            var sf = new AppFilter(project, organization);
            return await GetInternalAsync(sf, ti, $"type:{Event.KnownTypes.Session} {filter}", sort, mode, page, limit, after, true);
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
        [Consumes("application/json")]
        [ConfigurationResponseFilter]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<IActionResult> SetUserDescriptionAsync(string referenceId, UserDescription description, string projectId = null) {
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

        [Obsolete]
        [HttpPatch("~/api/v1/error/{id:objectid}")]
        [Consumes("application/json")]
        [ConfigurationResponseFilter]
        public async Task<IActionResult> LegacyPatchAsync(string id, Delta<UpdateEvent> changes) {
            if (changes == null)
                return Ok();

            if (changes.UnknownProperties.TryGetValue("UserEmail", out object value))
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
        public async Task<IActionResult> RecordHeartbeatAsync(string id = null, bool close = false) {
            if (_appOptions.EventSubmissionDisabled || String.IsNullOrEmpty(id))
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
                if (projectId != _appOptions.InternalProjectId) {
                    using (_logger.BeginScope(new ExceptionlessState().Project(projectId).Identity(CurrentUser?.EmailAddress).Property("User", CurrentUser).Property("Id", id).Property("Close", close).SetHttpContext(HttpContext)))
                        _logger.LogError(ex, "Error enqueuing session heartbeat.");
                }

                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        [Obsolete]
        [HttpGet("~/api/v1/events/submit")]
        [HttpGet("~/api/v1/events/submit/{type:minlength(1)}")]
        [HttpGet("~/api/v1/projects/{projectId:objectid}/events/submit")]
        [HttpGet("~/api/v1/projects/{projectId:objectid}/events/submit/{type:minlength(1)}")]
        [ConfigurationResponseFilter]
        public Task<ActionResult> GetSubmitEventV1Async(string projectId = null, string type = null, [FromHeader][UserAgent] string userAgent = null, [FromQuery][QueryStringParameters] IQueryCollection parameters = null) {
            return GetSubmitEventAsync(projectId, 1, type, userAgent, parameters);
        }

        /// <summary>
        /// Submit event by GET
        /// </summary>
        /// <remarks>
        /// You can submit an event using an HTTP GET and query string parameters. Any unknown query string parameters will be added to the extended data of the event.
        ///
        /// Feature usage named build with a duration of 10:
        /// <code><![CDATA[/events/submit?access_token=YOUR_API_KEY&type=usage&source=build&value=10]]></code>
        ///
        /// Log with message, geo and extended data
        /// <code><![CDATA[/events/submit?access_token=YOUR_API_KEY&type=log&message=Hello World&source=server01&geo=32.85,-96.9613&randomproperty=true]]></code>
        /// </remarks>
        /// <param name="type">The event type (ie. error, log message, feature usage).</param>
        /// <param name="source">The event source (ie. machine name, log name, feature name).</param>
        /// <param name="message">The event message.</param>
        /// <param name="reference">An optional identifier to be used for referencing this event instance at a later time.</param>
        /// <param name="date">The date that the event occurred on.</param>
        /// <param name="count">The number of duplicated events.</param>
        /// <param name="value">The value of the event if any.</param>
        /// <param name="geo">The geo coordinates where the event happened.</param>
        /// <param name="tags">A list of tags used to categorize this event (comma separated).</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        /// <param name="identityname">The user's friendly name that the event happened to.</param>
        /// <param name="userAgent">The user agent that submitted the event.</param>
        /// <param name="parameters">Query string parameters that control what properties are set on the event</param>
        /// <response code="200">OK</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpGet("submit")]
        [ConfigurationResponseFilter]
        public Task<ActionResult> GetSubmitEventV2Async(string type = null, string source = null, string message = null, string reference = null,
            string date = null, int? count = null, decimal? value = null, string geo = null, string tags = null, string identity = null,
            string identityname = null, [FromHeader][UserAgent] string userAgent = null, [FromQuery][QueryStringParameters] IQueryCollection parameters = null) {
            return GetSubmitEventAsync(null, 2, null, userAgent, parameters);
        }

        /// <summary>
        /// Submit event type by GET
        /// </summary>
        /// <remarks>
        /// You can submit an event using an HTTP GET and query string parameters.
        ///
        /// Feature usage event named build with a value of 10:
        /// <code><![CDATA[/events/submit/usage?access_token=YOUR_API_KEY&source=build&value=10]]></code>
        ///
        /// Log event with message, geo and extended data
        /// <code><![CDATA[/events/submit/log?access_token=YOUR_API_KEY&message=Hello World&source=server01&geo=32.85,-96.9613&randomproperty=true]]></code>
        /// </remarks>
        /// <param name="type">The event type (ie. error, log message, feature usage).</param>
        /// <param name="source">The event source (ie. machine name, log name, feature name).</param>
        /// <param name="message">The event message.</param>
        /// <param name="reference">An optional identifier to be used for referencing this event instance at a later time.</param>
        /// <param name="date">The date that the event occurred on.</param>
        /// <param name="count">The number of duplicated events.</param>
        /// <param name="value">The value of the event if any.</param>
        /// <param name="geo">The geo coordinates where the event happened.</param>
        /// <param name="tags">A list of tags used to categorize this event (comma separated).</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        /// <param name="identityname">The user's friendly name that the event happened to.</param>
        /// <param name="userAgent">The user agent that submitted the event.</param>
        /// <param name="parameters">Query string parameters that control what properties are set on the event</param>
        /// <response code="200">OK</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpGet("submit/{type:minlength(1)}")]
        [ConfigurationResponseFilter]
        public Task<ActionResult> GetSubmitEventByTypeV2Async(string type, string source = null, string message = null, string reference = null,
            string date = null, int? count = null, decimal? value = null, string geo = null, string tags = null, string identity = null,
            string identityname = null, [FromHeader][UserAgent] string userAgent = null, [FromQuery][QueryStringParameters] IQueryCollection parameters = null) {
            return GetSubmitEventAsync(null, 2, type, userAgent, parameters);
        }

        /// <summary>
        /// Submit event type by GET for a specific project
        /// </summary>
        /// <remarks>
        /// You can submit an event using an HTTP GET and query string parameters.
        ///
        /// Feature usage named build with a duration of 10:
        /// <code><![CDATA[/projects/{projectId}/events/submit?access_token=YOUR_API_KEY&type=usage&source=build&value=10]]></code>
        ///
        /// Log with message, geo and extended data
        /// <code><![CDATA[/projects/{projectId}/events/submit?access_token=YOUR_API_KEY&type=log&message=Hello World&source=server01&geo=32.85,-96.9613&randomproperty=true]]></code>
        /// </remarks>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="type">The event type (ie. error, log message, feature usage).</param>
        /// <param name="source">The event source (ie. machine name, log name, feature name).</param>
        /// <param name="message">The event message.</param>
        /// <param name="reference">An optional identifier to be used for referencing this event instance at a later time.</param>
        /// <param name="date">The date that the event occurred on.</param>
        /// <param name="count">The number of duplicated events.</param>
        /// <param name="value">The value of the event if any.</param>
        /// <param name="geo">The geo coordinates where the event happened.</param>
        /// <param name="tags">A list of tags used to categorize this event (comma separated).</param>
        /// <param name="identity">The user's identity that the event happened to.</param>
        /// <param name="identityname">The user's friendly name that the event happened to.</param>
        /// <param name="userAgent">The user agent that submitted the event.</param>
        /// <param name="parameters">Query String parameters that control what properties are set on the event</param>
        /// <response code="200">OK</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpGet("~/api/v2/projects/{projectId:objectid}/events/submit")]
        [HttpGet("~/api/v2/projects/{projectId:objectid}/events/submit/{type:minlength(1)}")]
        [ConfigurationResponseFilter]
        public Task<ActionResult> GetSubmitEventByProjectV2Async(string projectId, string type = null, string source = null, string message = null, string reference = null,
            string date = null, int? count = null, decimal? value = null, string geo = null, string tags = null, string identity = null,
            string identityname = null, [FromHeader][UserAgent] string userAgent = null, [FromQuery][QueryStringParameters] IQueryCollection parameters = null) {
            return GetSubmitEventAsync(projectId, 2, type, userAgent, parameters);
        }

        private async Task<ActionResult> GetSubmitEventAsync(string projectId = null, int apiVersion = 2, string type = null, string userAgent = null, IQueryCollection parameters = null) {
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
                _logger.ProjectRouteDoesNotMatch(project?.Id, projectId);

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
                        if (Int32.TryParse(kvp.Value.FirstOrDefault(), out int intValue))
                            ev.Count = intValue;
                        break;
                    case "value":
                        if (Decimal.TryParse(kvp.Value.FirstOrDefault(), out decimal decValue))
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

                        if (kvp.Value.Count > 1)
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
                await _eventPostService.EnqueueAsync(new EventPost(_appOptions.EnableArchive) {
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
                if (projectId != _appOptions.InternalProjectId) {
                    using (_logger.BeginScope(new ExceptionlessState().Project(projectId).Identity(CurrentUser?.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                        _logger.LogError(ex, "Error enqueuing event post.");
                }

                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        [Obsolete]
        [HttpPost("~/api/v1/error")]
        [Consumes("application/json", "text/plain")]
        [RequestBodyContentAttribute]
        [ConfigurationResponseFilter]
        public Task<IActionResult> LegacyPostAsync([FromHeader][UserAgent] string userAgent = null) {
            return PostAsync(null, 1, userAgent);
        }

        [Obsolete]
        [HttpPost("~/api/v1/events")]
        [HttpPost("~/api/v1/projects/{projectId:objectid}/events")]
        [Consumes("application/json", "text/plain")]
        [RequestBodyContentAttribute]
        [ConfigurationResponseFilter]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task <IActionResult> PostV1Async(string projectId = null, [FromHeader][UserAgent]string userAgent = null) {
            return PostAsync(projectId, 1, userAgent);
        }

        ///  <summary>
        ///  Submit event by POST
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
        ///          "date":"2030-01-01T12:00:00.0000000-05:00",
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
        ///          "date":"2030-01-01T12:00:00.0000000-05:00",
        ///          "@simple_error": {
        ///              "message": "Simple Exception",
        ///              "type": "System.Exception",
        ///              "stack_trace": "   at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests.cs:line 77"
        ///          }
        ///      }
        ///  </code>
        ///  </remarks>
        ///  <param name="userAgent">The user agent that submitted the event.</param>
        ///  <response code="202">Accepted</response>
        ///  <response code="400">No project id specified and no default project was found.</response>
        ///  <response code="404">No project was found.</response>
        [HttpPost]
        [Consumes("application/json", "text/plain")]
        [RequestBodyContentAttribute]
        [ConfigurationResponseFilter]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task<IActionResult> PostV2Async([FromHeader][UserAgent]string userAgent = null) {
            return PostAsync(null, 2, userAgent);
        }

        ///  <summary>
        ///  Submit event by POST for a specific project
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
        ///          "date":"2030-01-01T12:00:00.0000000-05:00",
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
        ///          "date":"2030-01-01T12:00:00.0000000-05:00",
        ///          "@simple_error": {
        ///              "message": "Simple Exception",
        ///              "type": "System.Exception",
        ///              "stack_trace": "   at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests.cs:line 77"
        ///          }
        ///      }
        ///  </code>
        ///  </remarks>
        ///  <param name="projectId">The identifier of the project.</param>
        ///  <param name="userAgent">The user agent that submitted the event.</param>
        ///  <response code="202">Accepted</response>
        ///  <response code="400">No project id specified and no default project was found.</response>
        ///  <response code="404">No project was found.</response>
        [HttpPost("~/api/v2/projects/{projectId:objectid}/events")]
        [Consumes("application/json", "text/plain")]
        [RequestBodyContentAttribute]
        [ConfigurationResponseFilter]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task <IActionResult> PostByProjectV2Async(string projectId = null, [FromHeader][UserAgent]string userAgent = null) {
            return PostAsync(projectId, 2, userAgent);
        }

        private async Task <IActionResult> PostAsync(string projectId = null, int apiVersion = 2, [FromHeader][UserAgent]string userAgent = null) {
            if (Request.ContentLength.HasValue && Request.ContentLength.Value <= 0)
                return StatusCode(StatusCodes.Status202Accepted);

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = Request.GetProject();
            if (!String.Equals(project?.Id, projectId)) {
                _logger.ProjectRouteDoesNotMatch(project?.Id, projectId);

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

                await _eventPostService.EnqueueAsync(new EventPost(_appOptions.EnableArchive) {
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
                if (projectId != _appOptions.InternalProjectId) {
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
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        private Task<Organization> GetOrganizationAsync(string organizationId, bool useCache = true) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Task.FromResult<Organization>(null);

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

        private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(List<Stack> stacks, IReadOnlyCollection<KeyedBucket<string>> stackTerms, AppFilter sf, TimeInfo ti) {
            if (stacks.Count == 0)
                return new List<StackSummaryModel>(0);

            var totalUsers = await GetUserCountByProjectIdsAsync(stacks, sf, ti.Range.UtcStart, ti.Range.UtcEnd);
            return stacks.Join(stackTerms, s => s.Id, tk => tk.Key, (stack, term) => {
                var data = _formattingPluginManager.GetStackSummaryData(stack);
                var summary = new StackSummaryModel {
                    TemplateKey = data.TemplateKey,
                    Data = data.Data,
                    Id = stack.Id,
                    Title = stack.Title,
                    Status = stack.Status,
                    FirstOccurrence = term.Aggregations.Min<DateTime>("min_date").Value,
                    LastOccurrence = term.Aggregations.Max<DateTime>("max_date").Value,
                    Total = (long)(term.Aggregations.Sum("sum_count").Value ?? term.Total.GetValueOrDefault()),

                    Users = term.Aggregations.Cardinality("cardinality_user").Value.GetValueOrDefault(),
                    TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
                };

                return summary;
            }).ToList();
        }

        private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, AppFilter sf, DateTime utcStart, DateTime utcEnd) {
            var scopedCacheClient = new ScopedCacheClient(_cache, $"Project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
            var projectIds = stacks.Select(s => s.ProjectId).Distinct().ToList();
            var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);

            var totals = cachedTotals.Where(kvp => kvp.Value.HasValue).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
            if (totals.Count == projectIds.Count)
                return totals;

            var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date).Index(utcStart, utcEnd);
            var projects = cachedTotals.Where(kvp => !kvp.Value.HasValue).Select(kvp => new Project { Id = kvp.Key, OrganizationId = stacks.FirstOrDefault(s => s.ProjectId == kvp.Key)?.OrganizationId }).ToList();
            var countResult = await _repository.CountAsync(q => q.SystemFilter(systemFilter).FilterExpression(projects.BuildFilter()).EnforceEventStackFilter().AggregationsExpression("terms:(project_id cardinality:user)"));

            // Cache all projects that have more than 10 users for 5 minutes.
            var projectTerms = countResult.Aggregations.Terms<string>("terms_project_id").Buckets;
            var aggregations = projectTerms.ToDictionary(t => t.Key, t => t.Aggregations.Cardinality("cardinality_user").Value.GetValueOrDefault());
            await scopedCacheClient.SetAllAsync(aggregations.Where(t => t.Value >= 10).ToDictionary(k => k.Key, v => v.Value), TimeSpan.FromMinutes(5));
            totals.AddRange(aggregations);

            return totals;
        }
    }
}
