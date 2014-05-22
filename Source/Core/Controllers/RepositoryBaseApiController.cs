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
using Exceptionless.Core.Authorization;
using Exceptionless.Models;
using MongoDB.Driver;

namespace Exceptionless.Core.Controllers {
    // TODO: Look into batch (and some day options) support: 
    // http://bradwilson.typepad.com/blog/2012/06/batching-handler-for-web-api.html
    // http://trocolate.wordpress.com/2012/07/19/mitigate-issue-260-in-batching-scenario/
    // http://www.jefclaes.be/2012/09/supporting-options-verb-in-aspnet-web.html

    [Authorize]
    public abstract class RepositoryBaseApiController<TModel> : ExceptionlessApiController where TModel : class, IIdentity, new() {
        protected RepositoryBaseApiController() {
            string typeName = typeof(TModel).Name;
            _recordNotFoundResponseMessage = String.Concat(typeName, " with id '{0}' was not found.");
            _duplicateResponseMessage = String.Concat(typeName, " with id '{0}' already exists in the collection.");
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public abstract IEnumerable<TModel> Get();

        [Authorize(Roles = AuthorizationRoles.User)]
        public virtual TModel Get(string id) {
            TModel item = GetEntity(id);
            if (item == null)
                throw new HttpResponseException(NotFoundErrorResponseMessage(id));

            return item;
        }

        public virtual HttpResponseMessage Post(TModel value) {
            if (value == null)
                return BadRequestErrorResponseMessage();

            TModel model = null;
            try {
                model = InsertEntity(value);
            } catch (WriteConcernException) {
                return DuplicateResponseMessage(model != null ? model.Id : value.Id);
            }

            if (model == null || Request == null)
                return new HttpResponseMessage(HttpStatusCode.Created);

            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Created, value);
            response.Headers.Location = new Uri(Url.Link("DefaultApi", new {
                id = model.Id
            }));

            return response;
        }

        // TODO: Investigate work around for: http://aspnetwebstack.codeplex.com/workitem/562
        [Authorize(Roles = AuthorizationRoles.User)]
        public virtual HttpResponseMessage Put(string id, Web.OData.Delta<TModel> value) {
            if (value == null || !value.GetChangedPropertyNames().Any())
                return BadRequestErrorResponseMessage();

            TModel original = GetEntity(id);
            if (original == null)
                return NotFoundErrorResponseMessage(id);

            if (!CanUpdateEntity(original, value))
                return CannotUpdateReadOnlyPropertyResponseMessage();

            UpdateEntity(original, value);

            // Acceptable status codes are 200/201/204
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        // TODO: Investigate work around for: http://aspnetwebstack.codeplex.com/workitem/562
        public virtual HttpResponseMessage Patch(string id, Web.OData.Delta<TModel> value) {
            if (value == null || !value.GetChangedPropertyNames().Any())
                return BadRequestErrorResponseMessage();

            TModel original = GetEntity(id);
            if (original == null)
                return NotFoundErrorResponseMessage(id);

            if (!CanUpdateEntity(original, value))
                return CannotUpdateReadOnlyPropertyResponseMessage();

            UpdateEntity(original, value);

            return new HttpResponseMessage(HttpStatusCode.OK); // NoContent
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public virtual HttpResponseMessage Delete(string id) {
            TModel item = GetEntity(id);
            if (item == null)
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            DeleteEntity(item);

            // Acceptable status codes are 200/202/204
            return new HttpResponseMessage(HttpStatusCode.OK); // Accepted
        }

        /// <summary>
        /// Gets a record by a specific id.
        /// </summary>
        /// <param name="id">The documents id.</param>
        protected abstract TModel GetEntity(string id);

        /// <summary>
        /// Inserts a document.
        /// </summary>
        /// <param name="value">The document.</param>
        protected abstract TModel InsertEntity(TModel value); // TODO: User can currently specify the organization id to insert.

        /// <summary>
        /// A check to see if a document can be updated.
        /// </summary>
        /// <param name="original">The original document.</param>
        /// <param name="value">The document.</param>
        protected virtual bool CanUpdateEntity(TModel original, Web.OData.Delta<TModel> value) {
            return value.GetChangedPropertyNames().Any();
        }

        /// <summary>
        /// Updates a document.
        /// </summary>
        /// <param name="original">The original document.</param>
        /// <param name="value">The document.</param>
        protected abstract TModel UpdateEntity(TModel original, Web.OData.Delta<TModel> value);

        /// <summary>
        /// Deletes a document.
        /// </summary>
        /// <param name="value">The document.</param>
        /// >
        protected abstract void DeleteEntity(TModel value);
    }
}