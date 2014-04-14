using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;

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
        public IEnumerable<string> Get() {
            var results = _eventRepository.All();
            return new string[] { "value1", "value2" };
        }

        [Route("{id}")]
        public string Get(int id) {
            return "value";
        }

        [Route]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public async Task<IHttpActionResult> Post() {
            byte[] data = await Request.Content.ReadAsByteArrayAsync();
            await _eventPostQueue.EnqueueAsync(new EventPost {
                ContentType = Request.Content.Headers.ContentType.ToString(),
                ProjectId = ((ClaimsIdentity)User.Identity).FindFirst(ClaimTypes.NameIdentifier).Value,
                Data = data
            });
            //switch (data.GetJsonType()) {
            //    case JsonType.None:
            //    _serviceBus.Publish(EventPost);
            //        _eventRepository.Add(new Event {
            //            ProjectId = ObjectId.GenerateNewId().ToString(),
            //            OrganizationId = ObjectId.GenerateNewId().ToString(),
            //            Date = DateTimeOffset.Now,
            //            Type = "log",
            //            Message = data
            //        });
            //        break;
            //    case JsonType.Object:
            //        break;
            //    case JsonType.Array:
            //        break;
            //}

            return Ok();
        }
    }
}