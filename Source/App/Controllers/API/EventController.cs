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
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CodeSmith.Core.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Core.Web.OData;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Bson;
using NLog.Fluent;
using ServiceStack.CacheAccess;
using ServiceStack.Messaging;

namespace Exceptionless.App.Controllers.API {
    [ConfigurationResponseFilter]
    public class EventController : RepositoryOwnedByOrganizationApiController<Event, IEventRepository> {
        private readonly ICacheClient _cacheClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IMessageFactory _messageFactory;
        private readonly IAppStatsClient _stats;

        public EventController(IEventRepository repository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cacheClient, IMessageFactory messageFactory, IAppStatsClient stats) : base(repository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _cacheClient = cacheClient;
            _messageFactory = messageFactory;
            _stats = stats;
        }

        [Inject]
        public IStackRepository StackRepository { get; set; }

        public override IEnumerable<Event> Get() {
            return base.Get().Select(e => e.ToProjectLocalTime(_projectRepository));
        }

        public override Event Get(string id) {
            return base.Get(id).ToProjectLocalTime(_projectRepository);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.UserOrClient)]
        public override HttpResponseMessage Post(Event value) {
            if (value == null)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid event posted.");

            if (_cacheClient.TryGet<bool>("ApiDisabled", false))
                return Request.CreateResponse(HttpStatusCode.ServiceUnavailable);

            _stats.Counter(StatNames.ErrorsSubmitted);

            if (User != null && User.Project != null) {
                value.ProjectId = User.Project.Id;
                value.OrganizationId = User.Project.OrganizationId;
            }

            if (value.Date == DateTimeOffset.MinValue)
                value.Date = DateTimeOffset.Now;

            string message = User == null ? String.Format("Inserting event '{0}'.", value.Id) : String.Format("Inserting event '{0}' with API key '{1}'.", value.Id, User.Identity.Name);
            var ri = value.GetRequestInfo();
            if (ri != null)
                message += String.Format(" IP Address: {0}.", ri.ClientIpAddress);
            //if (value.ExceptionlessClientInfo != null)
            //    message += String.Format(" Client Version: {0}.", value.ExceptionlessClientInfo.Version);
            Log.Debug().Message(message).Write();

            if (String.IsNullOrWhiteSpace(value.OrganizationId) || !User.IsInOrganization(value.OrganizationId))
                return InvalidOrganizationErrorResponseMessage();

            string id = value.Id;
            if (String.IsNullOrEmpty(id)) {
                value.Id = ObjectId.GenerateNewId().ToString();
                id = value.Id;
            }

            if (_messageFactory != null) {
                using (IMessageProducer messageProducer = _messageFactory.CreateMessageProducer()) {
                    RetryUtil.Retry(() => messageProducer.Publish(value));
                    _stats.Counter(StatNames.ErrorsQueued);
                }
            } else
                Log.Error().Message("Message Factory is null").Write();

            if (Request == null)
                return CreatedResponseMessage();

            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Created);
            response.Headers.Location = new Uri(Url.Link("DefaultApi", new {
                id
            }));
            return response;
        }

        [NonAction]
        public override HttpResponseMessage Delete(string id) {
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        protected override bool CanUpdateEntity(Event original, Delta<Event> value) {
            // TODO: Only let the client patch certain things.
            Event entity = value.GetEntity();
            if (value.ContainsChangedProperty(t => t.Date) && original.Date != entity.Date)
                return false;

            if (value.ContainsChangedProperty(t => t.StackId) && !String.Equals(original.StackId, entity.StackId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (value.ContainsChangedProperty(t => t.IsFixed) && original.IsFixed != entity.IsFixed)
                return false;

            if (value.ContainsChangedProperty(t => t.IsHidden) && original.IsHidden != entity.IsHidden)
                return false;

            return base.CanUpdateEntity(original, value);
        }

        // TODO: Use the cache client.
        [HttpGet]
        public IHttpActionResult Recent(string projectId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetByIdCached(projectId);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime retentionUtcCutoff = _organizationRepository.GetByIdCached(project.OrganizationId).GetRetentionUtcCutoff();
            DateTime utcStart = range.Item1 != DateTime.MinValue ? _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1) : DateTime.MinValue;
            DateTime utcEnd = range.Item2 != DateTime.MaxValue ? _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2) : DateTime.MaxValue;

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            List<Event> query = _repository.GetMostRecent(projectId, utcStart, utcEnd, skip, pageSize, hidden, @fixed, notfound).ToList();
            List<ErrorResult> models = query.Where(m => m.Date.UtcDateTime >= retentionUtcCutoff).Select(e => e.ToProjectLocalTime(project)).Select(Mapper.Map<Event, EventResult>).ToList();

            var result = new PlanPagedResult<ErrorResult>(models, query.Count > models.Count) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10,
            };

            // TODO: Only return the Exception Type properties type name without the namespace.
            // TODO: Only return the populated fields (currently all properties are being returned).
            return Ok(result);
        }

        // TODO: Use the cache client.
        [HttpGet]
        public IHttpActionResult GetByStack(string stackId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            var stack = StackRepository.GetByIdCached(stackId);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(stack.ProjectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(stack.ProjectId, range.Item2);

            Project project = _projectRepository.GetByIdCached(stack.ProjectId);
            DateTime retentionUtcCutoff = _organizationRepository.GetByIdCached(project.OrganizationId).GetRetentionUtcCutoff();

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            List<Event> query = _repository.GetByStackIdOccurrenceDate(stackId, utcStart, utcEnd, skip, pageSize).ToList();

            List<ErrorResult> models = query.Where(m => m.Date.UtcDateTime >= retentionUtcCutoff).Select(e => e.ToProjectLocalTime(_projectRepository)).Select(Mapper.Map<Event, EventResult>).ToList();

            var result = new PlanPagedResult<ErrorResult>(models, query.Count > models.Count) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            };

            // TODO: Only return the populated fields (currently all properties are being returned).
            return Ok(result);
        }
    }
}