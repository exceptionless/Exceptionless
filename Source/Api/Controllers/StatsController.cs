#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Http;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stats")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StatsController : ExceptionlessApiController {
        private readonly IStackRepository _stackRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly EventStats _stats;

        public StatsController(IStackRepository stackRepository, IProjectRepository projectRepository, EventStats stats) {
            _stackRepository = stackRepository;
            _projectRepository = projectRepository;
            _stats = stats;
        }

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string filter = null, string time = null, string offset = null) {
            return GetInternal(null, filter, time, offset);
        }

        public IHttpActionResult GetInternal(string systemFilter, string userFilter = null, string time = null, string offset = null) {
            // TODO: Handle UTC Retention Cutoff.
            var timeInfo = GetTimeInfo(time, offset);
            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetAssociatedOrganizationsFilter();
            var result = _stats.GetOccurrenceStats(timeInfo.UtcRange.Start, timeInfo.UtcRange.End, systemFilter, userFilter, timeInfo.Offset);

            return Ok(result);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stats")]
        public IHttpActionResult GetByProject(string projectId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, time, offset);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/stats")]
        public IHttpActionResult GetByStack(string stackId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            Stack stack = _stackRepository.GetById(stackId);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("stack:", stackId), filter, time, offset);
        }
    }
}