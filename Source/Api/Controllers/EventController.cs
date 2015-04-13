using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models.Data;
using FluentValidation;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Storage;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/events")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : RepositoryApiController<IEventRepository, PersistentEvent, PersistentEvent, PersistentEvent, UpdateEvent> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;
        private readonly IMetricsClient _metricsClient;
        private readonly IValidator<UserDescription> _userDescriptionValidator;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly IFileStorage _storage;

        public EventController(IEventRepository repository, 
            IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, 
            IStackRepository stackRepository,
            IQueue<EventPost> eventPostQueue, 
            IQueue<EventUserDescription> eventUserDescriptionQueue,
            IMetricsClient metricsClient,
            IValidator<UserDescription> userDescriptionValidator,
            FormattingPluginManager formattingPluginManager,
            IFileStorage storage) : base(repository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventPostQueue = eventPostQueue;
            _eventUserDescriptionQueue = eventUserDescriptionQueue;
            _metricsClient = metricsClient;
            _userDescriptionValidator = userDescriptionValidator;
            _formattingPluginManager = formattingPluginManager;
            _storage = storage;

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
        public IHttpActionResult GetById(string id, string filter = null, string time = null, string offset = null) {
            var model = GetModel(id);
            if (model == null)
                return NotFound();

            var organization = _organizationRepository.GetById(model.OrganizationId, true);
            if (organization.RetentionDays > 0 && model.Date.UtcDateTime < DateTime.UtcNow.SubtractDays(organization.RetentionDays))
                return PlanLimitReached("Unable to view event occurrence due to plan limits.");

            if (!String.IsNullOrEmpty(filter))
                filter = filter.ReplaceFirst("stack:current", "stack:" + model.StackId);

            var processResult = QueryProcessor.Process(filter);
            if (!processResult.IsValid)
                return OkWithLinks(model, GetEntityResourceLink<Stack>(model.StackId, "parent"));

            var systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, processResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(filter));

            var timeInfo = GetTimeInfo(time, offset);
            return OkWithLinks(model,
                GetEntityResourceLink(_repository.GetPreviousEventId(model, systemFilter, processResult.ExpandedQuery, timeInfo.UtcRange.Start, timeInfo.UtcRange.End), "previous"),
                GetEntityResourceLink(_repository.GetNextEventId(model, systemFilter, processResult.ExpandedQuery, timeInfo.UtcRange.Start, timeInfo.UtcRange.End), "next"),
                GetEntityResourceLink<Stack>(model.StackId, "parent"));
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
        public IHttpActionResult Get(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, sort, time, offset, mode, page, limit);
        }

        private IHttpActionResult GetInternal(string systemFilter = null, string userFilter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page + 1, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var processResult = QueryProcessor.Process(userFilter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, processResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter));

            var sortBy = GetSort(sort);
            var timeInfo = GetTimeInfo(time, offset);
            var options = new PagingOptions { Page = page, Limit = limit };

            ICollection<PersistentEvent> events;
            try {
                events = _repository.GetByFilter(systemFilter, processResult.ExpandedQuery, sortBy.Item1, sortBy.Item2, timeInfo.Field, timeInfo.UtcRange.Start, timeInfo.UtcRange.End, options);
            } catch (ApplicationException ex) {
                Log.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Sort = sort, Time = time, Offset = offset, Page = page, Limit = limit })
                    .Tag("Search")
                    .Property("User", ExceptionlessUser)
                    .ContextProperty("HttpActionContext", ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }
            
            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(events.Select(e => {
                    var summaryData = _formattingPluginManager.GetEventSummaryData(e);
                    return new EventSummaryModel {
                        TemplateKey = summaryData.TemplateKey,
                        Id = e.Id,
                        Date = e.Date,
                        Data = summaryData.Data
                    };
                }).ToList(), options.HasMore, page);

            return OkWithResourceLinks(events, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
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
        public IHttpActionResult GetByOrganization(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            return GetInternal(String.Concat("organization:", organizationId), filter, sort, time, offset, mode, page, limit);
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
        public IHttpActionResult GetByProject(string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, sort, time, offset, mode, page, limit);
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
        public IHttpActionResult GetByStack(string stackId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            var stack = _stackRepository.GetById(stackId, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("stack:", stackId), filter, sort, time, offset, mode, page, limit);
        }

        /// <summary>
        /// Get by reference id
        /// </summary>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("by-ref/{referenceId:minlength(8)}")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public IHttpActionResult GetByReferenceId(string referenceId) {
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();
            
            return GetInternal(userFilter: String.Concat("reference:", referenceId));
        }

        /// <summary>
        /// Get by reference id
        /// </summary>
        /// <param name="referenceId">An identifier used that references an event instance.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <response code="404">The event occurrence with the specified reference id could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:minlength(8)}")]
        [ResponseType(typeof(List<PersistentEvent>))]
        public IHttpActionResult GetByReferenceId(string referenceId, string projectId) {
            if (String.IsNullOrEmpty(referenceId) || String.IsNullOrEmpty(projectId))
                return NotFound();
            
            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), String.Concat("reference:", referenceId));
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
        [Route("by-ref/{referenceId:minlength(8)}/user-description")]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:minlength(8)}/user-description")]
        [OverrideAuthorization]
        //[Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        [ResponseType(typeof(List<PersistentEvent>))]
        public async Task<IHttpActionResult> SetUserDescriptionAsync(string referenceId, UserDescription description, string projectId = null) {
            await _metricsClient.CounterAsync(MetricNames.EventsUserDescriptionSubmitted);
            
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();

            if (description == null)
                return BadRequest("Description must be specified.");

            var result = _userDescriptionValidator.Validate(description);
            if (!result.IsValid)
                return BadRequest(result.Errors.ToErrorMessage());

            if (projectId == null)
                projectId = DefaultProject.Id;

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !User.GetOrganizationIds().ToList().Contains(project.OrganizationId))
                return NotFound();

            var eventUserDescription = Mapper.Map<UserDescription, EventUserDescription>(description);
            eventUserDescription.ProjectId = projectId;
            eventUserDescription.ReferenceId = referenceId;

            _eventUserDescriptionQueue.Enqueue(eventUserDescription);
            await _metricsClient.CounterAsync(MetricNames.EventsUserDescriptionQueued);

            return StatusCode(HttpStatusCode.Accepted);
        }

        [HttpPatch]
        [Route("~/api/v1/error/{id:objectid}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> LegacyPatch(string id, Delta<UpdateEvent> changes) {
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
        /// You can create an event by posting any uncompressed or compressed (gzip or deflate) string or json object. If we know how to handle it 
        /// we will create a new event. If none of the JSON properties match the event object then we will create a new event and place your JSON 
        /// object into the events data collection.
        /// 
        /// You can also post a multiline string. We automatically split strings by the \n character and create a new log event for every line.
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

            await _metricsClient.CounterAsync(MetricNames.PostsSubmitted);

            if (projectId == null)
                projectId = Request.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !Request.GetAssociatedOrganizationIds().Contains(project.OrganizationId))
                return NotFound();

            string contentEncoding = Request.Content.Headers.ContentEncoding.ToString();
            bool isCompressed = contentEncoding == "gzip" || contentEncoding == "deflate";
            if (!isCompressed && data.Length > 1000) {
                data = await data.CompressAsync();
                contentEncoding = "gzip";
            }

            try {
                await _eventPostQueue.EnqueueAsync(new EventPostInfo {
                    MediaType = Request.Content.Headers.ContentType != null ? Request.Content.Headers.ContentType.MediaType : null,
                    CharSet = Request.Content.Headers.ContentType != null ? Request.Content.Headers.ContentType.CharSet : null,
                    ProjectId = projectId,
                    UserAgent = userAgent,
                    ApiVersion = version,
                    Data = data,
                    ContentEncoding = contentEncoding
                }, _storage);
            } catch (Exception ex) {
                Log.Error().Exception(ex)
                    .Message("Error enqueuing event post.")
                    .Project(projectId)
                    .Property("User", ExceptionlessUser)
                    .ContextProperty("HttpActionContext", ActionContext)
                    .WriteIf(projectId != Settings.Current.InternalProjectId);

                // TODO: Change to async once vnext is released.
                _metricsClient.Counter(MetricNames.PostsQueuedErrors);
                return StatusCode(HttpStatusCode.InternalServerError);
            }

            await _metricsClient.CounterAsync(MetricNames.PostsQueued);
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
        public async Task<IHttpActionResult> DeleteAsync(string ids) {
            return await base.DeleteAsync(ids.FromDelimitedString());
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<UserDescription, EventUserDescription>() == null)
                Mapper.CreateMap<UserDescription, EventUserDescription>();

            base.CreateMaps();
        }
    }
}