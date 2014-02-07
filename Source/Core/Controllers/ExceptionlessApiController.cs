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
using System.Web.Http;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Web;

namespace Exceptionless.Core.Controllers {
    [RequireHttpsExceptLocal]
    public class ExceptionlessApiController : ApiController {
        protected string _recordNotFoundResponseMessage = "Resource not found.";
        protected string _duplicateResponseMessage = "Resource already exists in the collection.";

        protected new ExceptionlessPrincipal User { get { return base.User as ExceptionlessPrincipal; } }

        protected virtual HttpResponseMessage DuplicateResponseMessage(string id) {
            return Request != null && !String.IsNullOrEmpty(id) ? Request.CreateErrorResponse(HttpStatusCode.Conflict, String.Format(_duplicateResponseMessage, id)) : new HttpResponseMessage(HttpStatusCode.Conflict);
        }

        protected virtual HttpResponseMessage BadRequestErrorResponseMessage() {
            if (ModelState.IsValid)
                return Request != null ? Request.CreateErrorResponse(HttpStatusCode.BadRequest, "An error occurred while processing this request.") : new HttpResponseMessage(HttpStatusCode.BadRequest);

            return Request != null ? Request.CreateErrorResponse(HttpStatusCode.BadRequest, ModelState) : new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        protected virtual HttpResponseMessage CreatedResponseMessage(string message = "Created") {
            return Request != null ? Request.CreateErrorResponse(HttpStatusCode.Created, message) : new HttpResponseMessage(HttpStatusCode.Created);
        }

        protected virtual HttpResponseMessage CannotUpdateReadOnlyPropertyResponseMessage() {
            return Request != null ? Request.CreateErrorResponse(HttpStatusCode.Conflict, "One or more read-only properties have been modified.") : new HttpResponseMessage(HttpStatusCode.Conflict);
        }

        protected virtual HttpResponseMessage NotFoundErrorResponseMessage(string id) {
            return Request != null && !String.IsNullOrEmpty(id) ? Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(_recordNotFoundResponseMessage, id)) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        protected virtual HttpResponseMessage PlanLimitReached(string message = "Your plan limit has been reached. Please upgrade your plan.") {
            return Request != null ? Request.CreateErrorResponse(HttpStatusCode.UpgradeRequired, message) : new HttpResponseMessage(HttpStatusCode.UpgradeRequired);
        }
    }
}