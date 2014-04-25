using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Web;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "event")]
    [Authorize(Roles = AuthorizationRoles.UserOrClient)]
    public class EventController : ApiController {
        private const string API_PREFIX = "api/v{version:int=1}/";
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventPostQueue;

        public EventController(IEventRepository repository, IQueue<EventPost> eventPostQueue) {
            _eventRepository = repository;
            _eventPostQueue = eventPostQueue;
        }

        [Route]
        public IEnumerable<Event> Get() {
            return _eventRepository.All();
        }

        [Route("{id}")]
        public Event Get(string id) {
            return _eventRepository.GetByIdCached(id);
        }

        [Route]
        [ConfigurationResponseFilter]
        public async Task<IHttpActionResult> Post([NakedBody]byte[] data, string projectId = null, int version = 1, [UserAgent]string userAgent = null) {
            if (projectId == null) {
                var ctx = Request.GetOwinContext();
                if (ctx != null && ctx.Request != null && ctx.Request.User != null)
                    projectId = ctx.Request.User.GetApiKeyProjectId();
            }

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

            return Ok();
        }
    }
}