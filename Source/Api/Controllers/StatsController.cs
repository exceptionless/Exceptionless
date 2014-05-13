#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Caching;
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
        private readonly ICacheClient _cacheClient;

        public StatsController(EventStatsHelper statsHelper, IOrganizationRepository organizationRepository, IStackRepository stackRepository, IProjectRepository projectRepository, ICacheClient cacheClient) {
            _cacheClient = cacheClient;
            _statsHelper = statsHelper;
            _organizationRepository = organizationRepository;
            _stackRepository = stackRepository;
            _projectRepository = projectRepository;
        }

        [HttpGet]
        [Route("project/{projectId}")]
        public IHttpActionResult GetByProject(string projectId, DateTime? start = null, DateTime? end = null, int page = 1, int pageSize = 10, bool hidden = false, bool @fixed = false, bool notfound = true) {
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
            result.MostFrequent = Frequent(result.MostFrequent.Results, result.TotalLimitedByPlan, page, pageSize);
            result.MostRecent = RecentInternal(projectId, page, pageSize, start, end, hidden, @fixed, notfound);

            return Ok(result);
        }

        [HttpGet]
        [Route("project/{projectId}/recent")]
        public IHttpActionResult Recent(string projectId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            return Ok(RecentInternal(projectId, page, pageSize, start, end, hidden, @fixed, notfound));
        }

        public PlanPagedResult<EventStackResult> RecentInternal(string projectId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                throw new ArgumentException();

            var range = GetDateRange(start, end);
            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2);
            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();

            pageSize = GetLimit(pageSize);
            int skip = GetSkip(page, pageSize);

            long count;
            List<Stack> query = _stackRepository.GetMostRecent(projectId, utcStart, utcEnd, skip, pageSize, out count, hidden, @fixed, notfound).ToList();
            List<Stack> errorStacks = query.Where(es => es.LastOccurrence >= retentionUtcCutoff).ToList();

            var result = new PlanPagedResult<EventStackResult>(null, totalLimitedByPlan: query.Count - errorStacks.Count, totalCount: count);
            result.Results.AddRange(errorStacks.Select(s => new EventStackResult {
                Id = s.Id,
                Type = s.SignatureInfo.ContainsKey("ExceptionType") ? s.SignatureInfo["ExceptionType"] : null,
                Method = s.SignatureInfo.ContainsKey("Method") ? s.SignatureInfo["Method"] : null,
                Path = s.SignatureInfo.ContainsKey("Path") ? s.SignatureInfo["Path"] : null,
                Is404 = s.SignatureInfo.ContainsKey("Path"),
                Title = s.Title,
                Total = s.TotalOccurrences,
                First = s.FirstOccurrence,
                Last = s.LastOccurrence
            }));

            result.Page = page > 1 ? page : 1;
            result.PageSize = pageSize >= 1 ? pageSize : 10;

            return result;
        }

        [HttpGet]
        [Route("project/{projectId}/frequent")]
        public IHttpActionResult Frequent(string projectId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
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
            return Ok(Frequent(result.MostFrequent.Results, result.TotalLimitedByPlan, page, pageSize));
        }

        private PlanPagedResult<EventStackResult> Frequent(List<EventStackResult> result, long totalLimitedByPlan, int page = 1, int pageSize = 10) {
            pageSize = GetLimit(pageSize);
            int skip = GetSkip(page, pageSize);

            var ers = new PlanPagedResult<EventStackResult>(result.Skip(skip).Take(pageSize).ToList());
            var stacks = _stackRepository.GetByIds(ers.Results.Select(s => s.Id).ToList());
            foreach (EventStackResult stats in ers.Results.ToList()) {
                Stack stack = stacks.SingleOrDefault(s => s.Id == stats.Id);
                if (stack == null) {
                    ers.Results.RemoveAll(r => r.Id == stats.Id);
                    continue;
                }

                // Stat's Id and Total properties are already calculated in the Results.
                stats.Type = stack.SignatureInfo.ContainsKey("ExceptionType") ? stack.SignatureInfo["ExceptionType"] : null;
                stats.Method = stack.SignatureInfo.ContainsKey("Method") ? stack.SignatureInfo["Method"] : null;
                stats.Path = stack.SignatureInfo.ContainsKey("Path") ? stack.SignatureInfo["Path"] : null;
                stats.Is404 = stack.SignatureInfo.ContainsKey("Path");

                stats.Title = stack.Title;
                stats.First = stack.FirstOccurrence;
                stats.Last = stack.LastOccurrence;
            }

            ers.TotalLimitedByPlan = ers.Results.Count != pageSize ? totalLimitedByPlan : 0;
            ers.TotalCount = result.Count + totalLimitedByPlan;
            ers.Page = page > 1 ? page : 1;
            ers.PageSize = pageSize >= 1 ? pageSize : 10;

            return ers;
        }

        [HttpGet]
        [Route("stack/{stackId}")]
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

        [HttpGet]
        [Route("plans")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult Plans() {
            return Ok(_organizationRepository.GetBillingPlanStats());
        }
    }
}