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
using System.Runtime.InteropServices;
using System.Web.Http;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Web.Results;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RequireHttpsExceptLocal]
    public abstract class ExceptionlessApiController : ApiController {
        public const int API_CURRENT_VERSION = 2;
        public const string API_PREFIX = "api/v2";

        protected Tuple<DateTime, DateTime> GetDateRange(DateTime? starTime, DateTime? endTime) {
            if (starTime == null)
                starTime = DateTime.MinValue;

            if (endTime == null)
                endTime = DateTime.MaxValue;

            return starTime < endTime ? new Tuple<DateTime, DateTime>(starTime.Value, endTime.Value) : new Tuple<DateTime, DateTime>(endTime.Value, starTime.Value);
        }

        public User ExceptionlessUser {
            get { return Request.GetUser(); }
        }

        public Project Project {
            get { return Request.GetProject(); }
        }

        public AuthType AuthType {
            get { return User.GetAuthType(); }
        }

        public bool CanAccessOrganization(string organizationId) {
            return Request.CanAccessOrganization(organizationId);
        }

        public bool IsInOrganization(string organizationId) {
            return Request.IsInOrganization(organizationId);
        }

        public IEnumerable<string> GetAssociatedOrganizationIds() {
            return Request.GetAssociatedOrganizationIds();
        }

        public string GetDefaultOrganizationId() {
            return Request.GetDefaultOrganizationId();
        }

        public const int DEFAULT_LIMIT = 10;
        protected int GetLimit(int limit) {
            if (limit < 1)
                limit = DEFAULT_LIMIT;
            else if (limit > 100)
                limit = 100;

            return limit;
        }

        protected int GetSkip(int currentPage, int pageSize) {
            int skip = (currentPage - 1) * pageSize;
            if (skip < 0)
                skip = 0;

            return skip;
        }

        public PlanLimitReachedActionResult PlanLimitReached(string message) {
            return new PlanLimitReachedActionResult(message, Request);
        }

        public NotImplementedActionResult NotImplemented(string message) {
            return new NotImplementedActionResult(message, Request);
        }

        public OkWithHeadersContentResult<T> OkWithHeaders<T>(T content, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) {
            return new OkWithHeadersContentResult<T>(content, this, headers);
        }

        public OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(IList<TEntity> content, bool hasMore, Func<TEntity, string> pagePropertyAccessor = null) where TEntity : class {
            return new OkWithResourceLinks<TEntity>(content, this, hasMore, null, pagePropertyAccessor);
        }

        public OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(IList<TEntity> content, bool hasMore, int page) where TEntity : class {
            return new OkWithResourceLinks<TEntity>(content, this, hasMore, page);
        }
    }
}