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
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stats")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StatsController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly EventStats _stats;

        public StatsController(IOrganizationRepository organizationRepository, IStackRepository stackRepository, IProjectRepository projectRepository, EventStats stats) {
            _organizationRepository = organizationRepository;
            _stackRepository = stackRepository;
            _projectRepository = projectRepository;
            _stats = stats;
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stats")]
        public IHttpActionResult GetByProject(string projectId, DateTime? start = null, DateTime? end = null, string filter = null) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var org = _organizationRepository.GetById(project.OrganizationId, true);
            if (!start.HasValue || start.Value < org.GetRetentionUtcCutoff())
                start = org.GetRetentionUtcCutoff();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            var result = _stats.GetOccurrenceStats(range.Item1, range.Item2, String.Concat("project:", projectId, " ", filter), project.DefaultTimeZoneOffset());

            return Ok(result);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/stats")]
        public IHttpActionResult GetByStack(string stackId, DateTime? start = null, DateTime? end = null, string filter = null) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            Stack stack = _stackRepository.GetById(stackId);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            var org = _organizationRepository.GetById(stack.OrganizationId, true);
            var project = _projectRepository.GetById(stack.ProjectId, true);
            if (!start.HasValue || start.Value < org.GetRetentionUtcCutoff())
                start = org.GetRetentionUtcCutoff();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            var result = _stats.GetOccurrenceStats(range.Item1, range.Item2, String.Concat("stack:", stackId, " ", filter), project.DefaultTimeZoneOffset());

            return Ok(result);
        }
    }
}