using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/events")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : RepositoryApiController<IEventRepository, PersistentEvent, PersistentEvent, Event, UpdateEvent> {
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IAppStatsClient _statsClient;

        public EventController(IEventRepository repository, IProjectRepository projectRepository, IStackRepository stackRepository, IQueue<EventPost> eventPostQueue, IAppStatsClient statsClient) : base(repository) {
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventPostQueue = eventPostQueue;
            _statsClient = statsClient;
        }

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string before = null, string after = null, int limit = 10) {
            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByOrganizationIds(GetAssociatedOrganizationIds(), options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/events")]
        public IHttpActionResult GetByProjectId(string projectId, string before = null, string after = null, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByProjectId(projectId, options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/events")]
        public IHttpActionResult GetByStackId(string stackId, string before = null, string after = null, int limit = 10) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            var stack = _stackRepository.GetById(stackId, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByStackId(stackId, options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
        }

        [HttpGet]
        [Route("{id:objectid}")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [HttpPatch]
        [HttpPut]
        [Route("{id:objectid}")]
        [Route("~/api/v1/error/{id:objectid}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        [ConfigurationResponseFilter]
        public override IHttpActionResult Patch(string id, Delta<UpdateEvent> changes) {
            if (changes == null)
                return Ok();

            if (changes.UnknownProperties.ContainsKey("UserEmail"))
                changes.TrySetPropertyValue("EmailAddress", changes.UnknownProperties["UserEmail"]);
            if (changes.UnknownProperties.ContainsKey("UserDescription"))
                changes.TrySetPropertyValue("Description", changes.UnknownProperties["UserDescription"]);

            return base.Patch(id, changes);
        }

        protected override PersistentEvent UpdateModel(PersistentEvent original, Delta<UpdateEvent> changes) {
            if (!changes.ContainsChangedProperty(e => e.EmailAddress) && !changes.ContainsChangedProperty(e => e.Description))
                return original;

            var userDescription = original.Data.ContainsKey(Event.KnownDataKeys.UserDescription) ? original.Data[Event.KnownDataKeys.UserDescription] : null;
            if (userDescription == null) {
                userDescription = new UserDescription();
                original.Data.Add(Event.KnownDataKeys.UserDescription, userDescription);
            }
            changes.Patch(userDescription);
            original.Data[Event.KnownDataKeys.UserDescription] = original.Data[Event.KnownDataKeys.UserDescription].ToJson();

            _repository.Save(original);

            return original;
        }

        [HttpPost]
        [Route("~/api/v{version:int=1}/error")]
        [Route("~/api/v{version:int=1}/events")]
        [Route("~/api/v{version:int=1}/projects/{projectId:objectid}/events")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        [ConfigurationResponseFilter]
        public async Task<IHttpActionResult> Post([NakedBody]byte[] data, string projectId = null, int version = 1, [UserAgent]string userAgent = null) {
            _statsClient.Counter(StatNames.PostsSubmitted);
            if (projectId == null)
                projectId = User.GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return BadRequest("No project id specified and no default project was found.");

            var project = _projectRepository.GetById(projectId, true);
            if (project == null || !User.GetOrganizationIds().ToList().Contains(project.OrganizationId))
                return NotFound();

            string contentEncoding = Request.Content.Headers.ContentEncoding.ToString();
            bool isCompressed = contentEncoding == "gzip" || contentEncoding == "deflate";
            if (!isCompressed && data.Length > 1000) {
                data = data.Compress();
                contentEncoding = "gzip";
            }

            await _eventPostQueue.EnqueueAsync(new EventPost {
                MediaType = Request.Content.Headers.ContentType.MediaType,
                CharSet = Request.Content.Headers.ContentType.CharSet,
                ProjectId = projectId,
                UserAgent = userAgent,
                ApiVersion = version,
                Data = data,
                ContentEncoding = contentEncoding
            });
            _statsClient.Counter(StatNames.PostsQueued);

            return StatusCode(HttpStatusCode.Accepted);
        }
    }
}