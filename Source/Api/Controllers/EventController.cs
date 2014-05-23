using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Web;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/events")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : RepositoryApiController<IEventRepository, PersistentEvent, PersistentEvent, Event, Event> {
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

        [Route("~/api/v{version:int=1}/events")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        [HttpPost]
        [ConfigurationResponseFilter]
        public async Task<IHttpActionResult> Post([NakedBody]byte[] data, string projectId = null, int version = 1, [UserAgent]string userAgent = null) {
            _statsClient.Counter(StatNames.PostsSubmitted);
            if (projectId == null)
                projectId = GetDefaultProjectId();

            // must have a project id
            if (String.IsNullOrEmpty(projectId))
                return StatusCode(HttpStatusCode.Unauthorized);

            // TODO: Add a check to see if the project id is over it's project limits. If it is, then turn off the client.

            bool isCompressed = Request.Content.Headers.ContentEncoding.Contains("gzip");
            if (!isCompressed)
                data = data.Compress();

            await _eventPostQueue.EnqueueAsync(new EventPost {
                MediaType = Request.Content.Headers.ContentType.MediaType,
                CharSet = Request.Content.Headers.ContentType.CharSet,
                ProjectId = projectId,
                UserAgent = userAgent,
                ApiVersion = version,
                Data = data
            });
            _statsClient.Counter(StatNames.PostsQueued);

            return StatusCode(HttpStatusCode.Accepted);
        }

        private string GetDefaultProjectId() {
            var project = _projectRepository.GetByOrganizationId(GetDefaultOrganizationId()).FirstOrDefault();
            return project != null ? project.Id : null;
        }
    }
}