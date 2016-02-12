using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models.Data;
using FluentValidation;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Repositories.Models;
using Foundatio.Storage;
using Newtonsoft.Json;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/events")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : RepositoryApiController<IEventRepository, PersistentEvent, PersistentEvent, PersistentEvent, UpdateEvent> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;
        private readonly IValidator<UserDescription> _userDescriptionValidator;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly IFileStorage _storage;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public EventController(IEventRepository repository,
            IOrganizationRepository organizationRepository,
            IProjectRepository projectRepository,
            IStackRepository stackRepository,
            IQueue<EventPost> eventPostQueue,
            IQueue<EventUserDescription> eventUserDescriptionQueue,
            IValidator<UserDescription> userDescriptionValidator,
            FormattingPluginManager formattingPluginManager,
            IFileStorage storage,
            JsonSerializerSettings jsonSerializerSettings) : base(repository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventPostQueue = eventPostQueue;
            _eventUserDescriptionQueue = eventUserDescriptionQueue;
            _userDescriptionValidator = userDescriptionValidator;
            _formattingPluginManager = formattingPluginManager;
            _storage = storage;
            _jsonSerializerSettings = jsonSerializerSettings;

            AllowedFields.Add("date");
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
        [HttpGet]
        [Route("{id:objectid}", Name = "GetPersistentEventById")]
        [ResponseType(typeof(PersistentEvent))]
        public async Task<IHttpActionResult> GetByIdAsync(string id, string filter = null, string time = null, string offset = null) {
            var model = await GetModelAsync(id, false);
            if (model == null)
                return NotFound();

            var organization = await _organizationRepository.GetByIdAsync(model.OrganizationId, true);
            if (organization == null)
                return NotFound();

            if (organization.RetentionDays > 0 && model.Date.UtcDateTime < DateTime.UtcNow.SubtractDays(organization.RetentionDays))
                return PlanLimitReached("Unable to view event occurrence due to plan limits.");

            if (!String.IsNullOrEmpty(filter))
                filter = filter.ReplaceFirst("stack:current", "stack:" + model.StackId);

            var processResult = QueryProcessor.Process(filter);
            if (!processResult.IsValid)
                return OkWithLinks(model, GetEntityResourceLink<Stack>(model.StackId, "parent"));

            var systemFilter = await GetAssociatedOrganizationsFilterAsync(_organizationRepository, processResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(filter));
            var timeInfo = GetTimeInfo(time, offset);
            var result = await _repository.GetPreviousAndNextEventIdsAsync(model, systemFilter, processResult.ExpandedQuery, timeInfo.UtcRange.Start, timeInfo.UtcRange.End);

            return OkWithLinks(model, GetEntityResourceLink(result.Previous, "previous"), GetEntityResourceLink(result.Next, "next"), GetEntityResourceLink<Stack>(model.StackId, "parent"));
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
        [HttpGet]
        [Route]
        [ResponseType(typeof(List<PersistentEvent>))]
        public Task<IHttpActionResult> GetAsync(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternalAsync(null, filter, sort, time, offset, mode, page, limit);
        }

        private async Task<IHttpActionResult> GetInternalAsync(string systemFilter = null, string userFilter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10, bool usesPremiumFeatures = false) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page + 1, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var processResult = QueryProcessor.Process(userFilter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = await GetAssociatedOrganizationsFilterAsync(_organizationRepository, processResult.UsesPremiumFeatures || usesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter));

            var sortBy = GetSort(sort);
            var timeInfo = GetTimeInfo(time, offset);
            var options = new PagingOptions { Page = page, Limit = limit };

            FindResults<PersistentEvent> events;
            try {
                events = await _repository.GetByFilterAsync(systemFilter, processResult.ExpandedQuery, sortBy, timeInfo.Field, timeInfo.UtcRange.Start, timeInfo.UtcRange.End, options);
            } catch (ApplicationException ex) {
                Logger.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Sort = sort, Time = time, Offset = offset, Page = page, Limit = limit })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
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
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetByOrganizationAsync(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            return await GetInternalAsync(String.Concat("organization:", organizationId), filter, sort, time, offset, mode, page, limit);
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
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetByProjectAsync(string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetInternalAsync($"project:{projectId}", filter, sort, time, offset, mode, page, limit);
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
        /// <response code="404">The stack could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/events")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetByStackAsync(string stackId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            var stack = await _stackRepository.GetByIdAsync(stackId, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            return await GetInternalAsync(String.Concat("stack:", stackId), filter, sort, time, offset, mode, page, limit);
        }

        /// <summary>
        /// Get by reference id
        /// </summary>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("by-ref/{referenceId:identifier}")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetByReferenceIdAsync(string referenceId, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();

            return await GetInternalAsync(null, String.Concat("reference:", referenceId), null, null, offset, mode, page, limit);
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
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetByReferenceIdAsync(string referenceId, string projectId, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();

            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetInternalAsync($"project:{projectId}", String.Concat("reference:", referenceId), null, null, offset, mode, page, limit);
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
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("sessions/{sessionId:identifier}")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetBySessionIdAsync(string sessionId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return await GetInternalAsync(null, $"ref.session:{sessionId} {filter}", sort, time, offset, mode, page, limit, true);
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
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/sessions/{sessionId:identifier}")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetBySessionIdAsync(string sessionId, string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetInternalAsync($"project:{projectId}", $"ref.session:{sessionId} {filter}", sort, time, offset, mode, page, limit, true);
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
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("sessions")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetBySessionAsync(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return await GetInternalAsync(null, $"type:{Event.KnownTypes.Session} {filter}", sort, time, offset, mode, page, limit, true);
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
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/sessions")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> GetBySessionAsync(string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();
            
            return await GetInternalAsync($"project:{projectId}", $"type:{Event.KnownTypes.Session} {filter}", sort, time, offset, mode, page, limit, true);
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
        [HttpPost]
        [Route("by-ref/{referenceId:identifier}/user-description")]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}/user-description")]
        //[OverrideAuthorization]
        //[Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> SetUserDescriptionAsync(string referenceId, UserDescription description, string projectId = null) {
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();

            if (description == null)
                return BadRequest("Description must be specified.");

            var result = await _userDescriptionValidator.ValidateAsync(description);
            if (!result.IsValid)
                return BadRequest(result.Errors.ToErrorMessage());

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            // Set the project for the configuration response filter.
            Request.SetProject(project);

            var eventUserDescription = await MapAsync<EventUserDescription>(description);
            eventUserDescription.ProjectId = projectId;
            eventUserDescription.ReferenceId = referenceId;

            await _eventUserDescriptionQueue.EnqueueAsync(eventUserDescription);
            return StatusCode(HttpStatusCode.Accepted);
        }

        [HttpPatch]
        [Route("~/api/v1/error/{id:objectid}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> LegacyPatchAsync(string id, Delta<UpdateEvent> changes) {
            if (changes == null)
                return Ok();

            object value;
            if (changes.UnknownProperties.TryGetValue("UserEmail", out value))
                changes.TrySetPropertyValue("EmailAddress", value);
            if (changes.UnknownProperties.TryGetValue("UserDescription", out value))
                changes.TrySetPropertyValue("Description", value);

            var userDescription = new UserDescription();
            changes.Patch(userDescription);

            return await SetUserDescriptionAsync(id, userDescription);
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
        /// <param name="version">The api version that should be used</param>
        /// <param name="type">The event type</param>
        /// <param name="userAgent">The user agent that submitted the event.</param>
        /// <param name="parameters">Parameters that control what properties are set on the event</param>
        /// <response code="202">Accepted</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpGet]
        [Route("~/api/v{version:int=2}/events/submit")]
        [Route("~/api/v{version:int=2}/events/submit/{type:minlength(1)}")]
        [Route("~/api/v{version:int=2}/projects/{projectId:objectid}/events/submit")]
        [Route("~/api/v{version:int=2}/projects/{projectId:objectid}/events/submit/{type:minlength(1)}")]
        [OverrideAuthorization]
        [ConfigurationResponseFilter]
        [Authorize(Roles = AuthorizationRoles.Client)]
        public async Task<IHttpActionResult> GetSubmitEvent(string projectId = null, int version = 2, string type = null, [UserAgent] string userAgent = null, [QueryStringParameters] IDictionary<string, string[]> parameters = null) {
            if (parameters == null || parameters.Count == 0)
                return StatusCode(HttpStatusCode.OK);

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            // TODO: We could save some overhead if we set the project in the overage handler...
            // Set the project for the configuration response filter.
            Request.SetProject(project);

            string contentEncoding = Request.Content.Headers.ContentEncoding.ToString();
            var ev = new Event { Type = !String.IsNullOrEmpty(type) ? type : Event.KnownTypes.Log };

            var exclusions = project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions).ToList();
            foreach (var kvp in parameters.Where(p => !String.IsNullOrEmpty(p.Key) && !p.Value.All(String.IsNullOrEmpty))) {
                switch (kvp.Key.ToLower()) {
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
                        DateTimeOffset dtValue;
                        if (DateTimeOffset.TryParse(kvp.Value.FirstOrDefault(), out dtValue))
                            ev.Date = dtValue;
                        break;
                    case "value":
                        decimal decValue;
                        if (Decimal.TryParse(kvp.Value.FirstOrDefault(), out decValue))
                            ev.Value = decValue;
                        break;
                    case "geo":
                        GeoResult geo;
                        if (GeoResult.TryParse(kvp.Value.FirstOrDefault(), out geo))
                            ev.Geo = geo.ToString();
                        break;
                    case "tags":
                        ev.Tags.AddRange(kvp.Value.SelectMany(t => t.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)).Distinct());
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

            try {
                await _eventPostQueue.EnqueueAsync(new EventPostInfo {
                    MediaType = Request.Content.Headers.ContentType?.MediaType,
                    CharSet = Request.Content.Headers.ContentType?.CharSet,
                    ProjectId = projectId,
                    UserAgent = userAgent,
                    ApiVersion = version,
                    Data = Encoding.UTF8.GetBytes(ev.ToJson(Formatting.None, _jsonSerializerSettings)),
                    ContentEncoding = contentEncoding,
                    IpAddress = Request.GetClientIpAddress()
                }, _storage);
            } catch (Exception ex) {
                Logger.Error().Exception(ex)
                    .Message("Error enqueuing event post.")
                    .Project(projectId)
                    .Identity(ExceptionlessUser?.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext)
                    .WriteIf(projectId != Settings.Current.InternalProjectId);

                return StatusCode(HttpStatusCode.InternalServerError);
            }

            return StatusCode(HttpStatusCode.OK);
        }
        
        [HttpPost]
        [Route("~/entries")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public Task<IHttpActionResult> PostRaygunAsync([NakedBody] byte[] data) {
            return PostAsync(data, null, 1, "raygun");
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <remarks>
        /// You can create an event by posting any uncompressed or compressed (gzip or deflate) string or json object. If we know how to handle it
        /// we will create a new event. If none of the JSON properties match the event object then we will create a new event and place your JSON
        /// object into the events data collection.
        ///
        /// You can also post a multiline String. We automatically split strings by the \n character and create a new log event for every line.
        ///
        /// Simple event:
        /// <code>
        ///     { "message": "Exceptionless is amazing!" }
        /// </code>
        ///
        /// Multiple events from string content:
        /// <code>
        ///     Exceptionless is amazing!
        ///     Exceptionless is really amazing!
        /// </code>
        ///
        /// Simple error:
        /// <code>
        ///     {
        ///         "type": "error",
        ///         "@simple_error": {
        ///             "message": "Simple Exception",
        ///             "type": "System.Exception",
        ///             "stack_trace": "   at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests.cs:line 77"
        ///         }
        ///     }
        /// </code>
        /// </remarks>
        /// <param name="data">The raw data.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="version">The api version that should be used</param>
        /// <param name="userAgent">The user agent that submitted the event.</param>
        /// <response code="202">Accepted</response>
        /// <response code="400">No project id specified and no default project was found.</response>
        /// <response code="404">No project was found.</response>
        [HttpPost]
        [Route("~/api/v{version:int=1}/error")]
        [Route("~/api/v{version:int=2}/events")]
        [Route("~/api/v{version:int=2}/projects/{projectId:objectid}/events")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        public async Task <IHttpActionResult> PostAsync([NakedBody]byte[] data, string projectId = null, int version = 2, [UserAgent]string userAgent = null) {
            if (data == null || data.Length == 0)
                return StatusCode(HttpStatusCode.Accepted);

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            // TODO: We could save some overhead if we set the project in the overage handler...
            // Set the project for the configuration response filter.
            Request.SetProject(project);

            string contentEncoding = Request.Content.Headers.ContentEncoding.ToString();
            bool isCompressed = contentEncoding == "gzip" || contentEncoding == "deflate";
            if (!isCompressed && data.Length > 1000) {
                data = await data.CompressAsync();
                contentEncoding = "gzip";
            }

            try {
                await _eventPostQueue.EnqueueAsync(new EventPostInfo {
                    MediaType = Request.Content.Headers.ContentType?.MediaType,
                    CharSet = Request.Content.Headers.ContentType?.CharSet,
                    ProjectId = projectId,
                    UserAgent = userAgent,
                    ApiVersion = version,
                    Data = data,
                    ContentEncoding = contentEncoding,
                    IpAddress = Request.GetClientIpAddress()
                }, _storage);
            } catch (Exception ex) {
                Logger.Error().Exception(ex)
                    .Message("Error enqueuing event post.")
                    .Project(projectId)
                    .Identity(ExceptionlessUser?.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext)
                    .WriteIf(projectId != Settings.Current.InternalProjectId);

                return StatusCode(HttpStatusCode.InternalServerError);
            }

            return StatusCode(HttpStatusCode.Accepted);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of event identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more event occurrences were not found.</response>
        /// <response code="500">An error occurred while deleting one or more event occurrences.</response>
        [HttpDelete]
        [Route("{ids:objectids}")]
        public Task<IHttpActionResult> DeleteAsync(string ids) {
            return base.DeleteAsync(ids.FromDelimitedString());
        }

        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, useCache);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return null;

            return project;
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<UserDescription, EventUserDescription>() == null)
                Mapper.CreateMap<UserDescription, EventUserDescription>();

            base.CreateMaps();
        }
    }
}
