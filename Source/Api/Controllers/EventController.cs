using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using CodeSmith.Core.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Models;
using MongoDB.Bson;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix("api/v1/event")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class EventController : ApiController {
        private readonly IEventRepository _eventRepository;

        public EventController(IEventRepository repository) {
            _eventRepository = repository;
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
            string result = await Request.Content.ReadAsStringAsync();
            switch (result.GetJsonType()) {
                case JsonType.None:
                    _eventRepository.Add(new Event {
                        ProjectId = ObjectId.GenerateNewId().ToString(),
                        OrganizationId = ObjectId.GenerateNewId().ToString(),
                        Date = DateTimeOffset.Now,
                        Type = "log",
                        Message = result
                    });
                    break;
                case JsonType.Object:
                    break;
                case JsonType.Array:
                    break;
            }

            return Ok();
        }
    }
}