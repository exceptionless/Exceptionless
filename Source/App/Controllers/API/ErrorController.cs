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
using System.Net.Http;
using CodeSmith.Core.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Web;
using Exceptionless.Core.Web.OData;
using Exceptionless.Extensions;
using Exceptionless.Models.Legacy;
using MongoDB.Bson;
using NLog.Fluent;
using ServiceStack.CacheAccess;
using ServiceStack.Messaging;

namespace Exceptionless.App.Controllers.API {
    [ConfigurationResponseFilter]
    public class ErrorController : ExceptionlessApiController {
        private readonly ICacheClient _cacheClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IMessageFactory _messageFactory;
        private readonly IAppStatsClient _stats;

        public ErrorController(IEventRepository repository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ICacheClient cacheClient, IMessageFactory messageFactory, IAppStatsClient stats) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _cacheClient = cacheClient;
            _messageFactory = messageFactory;
            _stats = stats;
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.UserOrClient)]
        public HttpResponseMessage Post(Error value) {
            if (value == null)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid error posted.");

            if (_cacheClient.TryGet<bool>("ApiDisabled", false))
                return Request.CreateResponse(HttpStatusCode.ServiceUnavailable);

            if (Request == null)
                return CreatedResponseMessage();

            string id = Guid.NewGuid().ToString("N");

            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Created);
            response.Headers.Location = new Uri(Url.Link("DefaultApi", new { id }));
            return response;
        }

        // TODO: Add Patching.
        protected bool CanUpdateEntity(Error original, Delta<Error> value) {
            // TODO: Only let the client patch certain things.
            Error entity = value.GetEntity();
            if (value.ContainsChangedProperty(t => t.OccurrenceDate) && original.OccurrenceDate != entity.OccurrenceDate)
                return false;

            if (value.ContainsChangedProperty(t => t.ErrorStackId) && !String.Equals(original.ErrorStackId, entity.ErrorStackId, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}