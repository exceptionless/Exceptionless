using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Storage;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using FluentValidation;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/events")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : RepositoryApiController<IEventRepository, PersistentEvent, PersistentEvent, PersistentEvent, UpdateEvent> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;
        private readonly IAppStatsClient _statsClient;
        private readonly IValidator<UserDescription> _userDescriptionValidator;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly IFileStorage _storage;

        public EventController(IEventRepository repository, 
            IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, 
            IStackRepository stackRepository,
            IQueue<EventPost> eventPostQueue, 
            IQueue<EventUserDescription> eventUserDescriptionQueue,
            IAppStatsClient statsClient,
            IValidator<UserDescription> userDescriptionValidator,
            FormattingPluginManager formattingPluginManager,
            IFileStorage storage) : base(repository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventPostQueue = eventPostQueue;
            _eventUserDescriptionQueue = eventUserDescriptionQueue;
            _statsClient = statsClient;
            _userDescriptionValidator = userDescriptionValidator;
            _formattingPluginManager = formattingPluginManager;
            _storage = storage;

            AllowedFields.Add("date");
        }
        
        [HttpGet]
        [Route("{id:objectid}", Name = "GetPersistentEventById")]
        public IHttpActionResult GetById(string id, string filter = null, string time = null, string offset = null) {
            var model = GetModel(id);
            if (model == null)
                return NotFound();

            var timeInfo = GetTimeInfo(time, offset);
            var validationResult = QueryValidator.Validate(filter);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Message);

            var systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, validationResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(filter));

            return OkWithLinks(model,
                GetEntityResourceLink(_repository.GetPreviousEventId(model, systemFilter, filter, timeInfo.UtcRange.Start, timeInfo.UtcRange.End), "previous"),
                GetEntityResourceLink(_repository.GetNextEventId(model, systemFilter, filter, timeInfo.UtcRange.Start, timeInfo.UtcRange.End), "next"),
                GetEntityResourceLink<Stack>(model.StackId, "parent"));
        }

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, sort, time, offset, mode, page, limit);
        }

        public IHttpActionResult GetInternal(string systemFilter = null, string userFilter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page + 1, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var validationResult = QueryValidator.Validate(userFilter);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, validationResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter));

            var sortBy = GetSort(sort);
            var timeInfo = GetTimeInfo(time, offset);
            var options = new PagingOptions { Page = page, Limit = limit };
            var events = _repository.GetByFilter(systemFilter, userFilter, sortBy.Item1, sortBy.Item2, timeInfo.Field, timeInfo.UtcRange.Start, timeInfo.UtcRange.End, options);
            
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

        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/events")]
        public IHttpActionResult GetByOrganization(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            return GetInternal(String.Concat("organization:", organizationId), filter, sort, time, offset, mode, page, limit);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events")]
        public IHttpActionResult GetByProjectId(string projectId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, sort, time, offset, mode, page, limit);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/events")]
        public IHttpActionResult GetByStackId(string stackId, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            var stack = _stackRepository.GetById(stackId, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("stack:", stackId), filter, sort, time, offset, mode, page, limit);
        }

        [HttpGet]
        [Route("by-ref/{referenceId:minlength(8)}")]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:minlength(8)}")]
        public IHttpActionResult GetByReferenceId(string referenceId, string projectId = null) {
            if (String.IsNullOrEmpty(referenceId))
                return NotFound();
            
            if (projectId == null)
                projectId = DefaultProject.Id;

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), String.Concat("reference:", referenceId));
        }

        [HttpPost]
        [Route("by-ref/{referenceId:minlength(8)}/user-description")]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events/by-ref/{referenceId:minlength(8)}/user-description")]
        [OverrideAuthorization]
        //[Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        public IHttpActionResult SetUserDescription(string referenceId, UserDescription description, string projectId = null) {
            _statsClient.Counter(StatNames.EventsUserDescriptionSubmitted);
            
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
            _statsClient.Counter(StatNames.EventsUserDescriptionQueued);

            return StatusCode(HttpStatusCode.Accepted);
        }

        [HttpPatch]
        [Route("~/api/v1/error/{id:objectid}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        public IHttpActionResult LegacyPatch(string id, Delta<UpdateEvent> changes) {
            if (changes == null)
                return Ok();

            object value;
            if (changes.UnknownProperties.TryGetValue("UserEmail", out value))
                changes.TrySetPropertyValue("EmailAddress", value);
            if (changes.UnknownProperties.TryGetValue("UserDescription", out value))
                changes.TrySetPropertyValue("Description", value);

            var userDescription = new UserDescription();
            changes.Patch(userDescription);

            return SetUserDescription(id, userDescription);
        }

        [HttpPost]
        [Route("~/api/v{version:int=1}/error")]
        [Route("~/api/v{version:int=1}/events")]
        [Route("~/api/v{version:int=1}/projects/{projectId:objectid}/events")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ConfigurationResponseFilter]
        public IHttpActionResult Post([NakedBody]byte[] data, string projectId = null, int version = 1, [UserAgent]string userAgent = null) {
            _statsClient.Counter(StatNames.PostsSubmitted);
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
                data = data.Compress();
                contentEncoding = "gzip";
            }

            _eventPostQueue.Enqueue(new EventPostInfo {
                MediaType = Request.Content.Headers.ContentType.MediaType,
                CharSet = Request.Content.Headers.ContentType.CharSet,
                ProjectId = projectId,
                UserAgent = userAgent,
                ApiVersion = version,
                Data = data,
                ContentEncoding = contentEncoding
            }, _storage);
            _statsClient.Counter(StatNames.PostsQueued);

            return StatusCode(HttpStatusCode.Accepted);
        }

        [HttpDelete]
        [Route("{ids:objectids}")]
        public override Task<IHttpActionResult> Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<UserDescription, EventUserDescription>() == null)
                Mapper.CreateMap<UserDescription, EventUserDescription>();

            base.CreateMaps();
        }
    }
}