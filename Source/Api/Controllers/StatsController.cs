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
using Exceptionless.Models.Stats;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stats")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StatsController : ExceptionlessApiController {
        private readonly EventStatsHelper _statsHelper;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IProjectRepository _projectRepository;

        public StatsController(EventStatsHelper statsHelper, IOrganizationRepository organizationRepository, IStackRepository stackRepository, IProjectRepository projectRepository) {
            _statsHelper = statsHelper;
            _organizationRepository = organizationRepository;
            _stackRepository = stackRepository;
            _projectRepository = projectRepository;
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/project/{projectId:objectid}/stats")]
        public IHttpActionResult GetByProject(string projectId, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();
            ProjectEventStatsResult result = _statsHelper.GetProjectErrorStats(projectId, _projectRepository.GetDefaultTimeOffset(projectId), start, end, retentionUtcCutoff, hidden, @fixed, notfound);
            result.MostFrequent = null;
            result.MostRecent = null;

            return Ok(result);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stack/{stackId:objectid}/stats")]
        public IHttpActionResult GetByStack(string stackId, DateTime? start = null, DateTime? end = null) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            Stack stack = _stackRepository.GetById(stackId);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            Project project = _projectRepository.GetById(stack.ProjectId, true);
            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();
            return Ok(_statsHelper.GetStackStats(stackId, _projectRepository.GetDefaultTimeOffset(stack.ProjectId), start, end, retentionUtcCutoff));
        }
    }
}