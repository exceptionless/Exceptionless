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
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Controllers {
    public abstract class RepositoryOwnedByOrganizationApiController<TModel, TRepository> : RepositoryApiController<TModel, TRepository>
        where TModel : class, IOwnedByOrganization, new()
        where TRepository : class, IRepositoryOwnedByOrganization<TModel> {
        public RepositoryOwnedByOrganizationApiController(TRepository repository) : base(repository) {}

        public override IEnumerable<TModel> Get() {
            return _repository.GetByOrganizationIds(Request.GetAssociatedOrganizationIds()).Take(100);
        }

        protected override TModel GetEntity(string id) {
            if (String.IsNullOrWhiteSpace(id))
                return null;

            TModel entity = _repository.GetById(id);
            return entity != null && Request.CanAccessOrganization(entity.OrganizationId) ? entity : null;
        }

        protected override TModel InsertEntity(TModel value) {
            if (String.IsNullOrWhiteSpace(value.OrganizationId) || !Request.IsInOrganization(value.OrganizationId))
                throw new HttpResponseException(InvalidOrganizationErrorResponseMessage());

            return base.InsertEntity(value);
        }

        protected override bool CanUpdateEntity(TModel original, Web.OData.Delta<TModel> value) {
            if (value.ContainsChangedProperty(t => t.OrganizationId) && !String.Equals(original.OrganizationId, value.GetEntity().OrganizationId, StringComparison.OrdinalIgnoreCase))
                return false;

            return base.CanUpdateEntity(original, value);
        }

        protected override void DeleteEntity(TModel value) {
            if (String.IsNullOrWhiteSpace(value.OrganizationId) || !Request.CanAccessOrganization(value.OrganizationId))
                throw new HttpResponseException(InvalidOrganizationErrorResponseMessage());

            _repository.Delete(Query.And(Query.EQ(MongoRepositoryWithIdentity<TModel>.FieldNames.Id, new BsonObjectId(new ObjectId(value.Id))), Query.EQ(MongoRepositoryOwnedByOrganization<TModel>.FieldNames.OrganizationId, new BsonObjectId(new ObjectId(value.OrganizationId)))));
            _repository.InvalidateCache(value);
        }

        protected virtual HttpResponseMessage InvalidOrganizationErrorResponseMessage() {
            return Request != null
                ? Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid OrganizationId.")
                : new HttpResponseMessage(HttpStatusCode.BadRequest);
        }
    }
}