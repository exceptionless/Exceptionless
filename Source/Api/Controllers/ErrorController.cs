#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Web;
using Exceptionless.Extensions;
using Exceptionless.Models.Legacy;

namespace Exceptionless.Api.Controllers {
    [ConfigurationResponseFilter]
    [RoutePrefix("api/v1/error")]
    [Authorize(Roles = AuthorizationRoles.UserOrClient)]
    public class ErrorController : ExceptionlessApiController {
        private readonly ICacheClient _cacheClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IAppStatsClient _stats;

        public ErrorController(IEventRepository repository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cacheClient, IAppStatsClient stats) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _cacheClient = cacheClient;
            _stats = stats;
        }

        [Route]
        [HttpPost]
        public IHttpActionResult Post(Error value) {
            if (value == null)
                return BadRequest("Invalid error posted.");

            if (_cacheClient.TryGet("ApiDisabled", false))
                return StatusCode(HttpStatusCode.ServiceUnavailable);

            // TODO: Implement Post

            return Ok();
        }

        [Route]
        [HttpPatch]
        protected IHttpActionResult Patch(Error original, Error value) {
            // TODO: Add Patching and only let the client patch certain things.

            return Ok();
        }
    }
}