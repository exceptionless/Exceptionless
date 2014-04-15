using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "event")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : ApiController {
        private const string API_PREFIX = "api/v1/";
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
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public async Task<IHttpActionResult> Post() {
            byte[] data = await Request.Content.ReadAsByteArrayAsync();
            await _eventPostQueue.EnqueueAsync(new EventPost {
                ContentType = Request.Content.Headers.ContentType.ToString(),
                ContentEncoding = Request.Content.Headers.ContentEncoding.ToString(),
                ProjectId = ((ClaimsIdentity)User.Identity).FindFirst(ClaimTypes.NameIdentifier).Value,
                Data = data
            });

            return Ok();
        }
    }
}