using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/event")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : RepositoryApiController<EventRepository, PersistentEvent, PersistentEvent, Event, Event> {
        private readonly IProjectRepository _projectRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IAppStatsClient _statsClient;

        public EventController(EventRepository repository, IProjectRepository projectRepository, IQueue<EventPost> eventPostQueue, IAppStatsClient statsClient) : base(repository) {
            _projectRepository = projectRepository;
            _eventPostQueue = eventPostQueue;
            _statsClient = statsClient;
        }

        #region CRUD

        [HttpGet]
        [Route]
        public override IHttpActionResult Get(string organization = null, string before = null, string after = null, int limit = 10) {
            var options = GetOptions(before, after, limit);
            options.OrganizationId = organization;

            var results = GetEntities<PersistentEvent>(options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stack/{stackId}/event")]
        public IHttpActionResult GetByStackId(string stackId, string before = null, string after = null, int limit = 10) {
            var options = GetOptions(before, after, limit);
            options.StackId = stackId;

            var results = GetEntities<PersistentEvent>(options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/project/{projectId}/event")]
        public IHttpActionResult GetByProjectId(string projectId, string before = null, string after = null, int limit = 10) {
            var options = GetOptions(before, after, limit);
            options.ProjectId = projectId;

            var results = GetEntities<PersistentEvent>(options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
        }

        private GetEntitiesOptions GetOptions(string before, string after, int limit) {
            var options = new GetEntitiesOptions { Limit = limit, SortBy = SortBy.Descending(EventRepository.FieldNames.Date_UTC) };

            DateTime beforeDate, afterDate;
            if (DateTime.TryParse(before, out beforeDate))
                options.BeforeQuery = Query.LT(EventRepository.FieldNames.Date_UTC, beforeDate.Ticks);
            if (DateTime.TryParse(after, out afterDate))
                options.AfterQuery = Query.GT(EventRepository.FieldNames.Date_UTC, afterDate.Ticks);

            return options;
        }

        [HttpGet]
        [Route("{id}")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        #endregion

        [Route("~/api/v{version:int=1}/event")]
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

            return Ok();
        }

        private string GetDefaultProjectId() {
            var project = _projectRepository.GetByOrganizationId(GetDefaultOrganizationId()).FirstOrDefault();
            return project != null ? project.Id : null;
        }
    }
}