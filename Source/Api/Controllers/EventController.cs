using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Web;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix("api/v{version:int=1}/event")]
    [Authorize(Roles = AuthorizationRoles.UserOrClient)]
    public class EventController : ExceptionlessApiController {
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IAppStatsClient _statsClient;

        public EventController(IEventRepository repository, IQueue<EventPost> eventPostQueue, IAppStatsClient statsClient) {
            _eventRepository = repository;
            _eventPostQueue = eventPostQueue;
            _statsClient = statsClient;
        }

        [Route]
        [HttpGet]
        public IEnumerable<Event> Get() {
            // TODO: Limit by active user.
            return _eventRepository.All();
        }

        [HttpGet]
        [Route("{id}")]
        public Event Get(string id) {
            // TODO: Limit by active user.
            return _eventRepository.GetByIdCached(id);
        }

        [Route]
        [HttpPost]
        [ConfigurationResponseFilter]
        public async Task<IHttpActionResult> Post([NakedBody]byte[] data, string projectId = null, int version = 1, [UserAgent]string userAgent = null) {
            _statsClient.Counter(StatNames.PostsSubmitted);
            if (projectId == null)
                projectId = User.GetProjectId();

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
    }
}