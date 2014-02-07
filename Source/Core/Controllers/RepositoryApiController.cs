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
using Exceptionless.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Core.Controllers {
    public abstract class RepositoryApiController<TModel, TRepository> : RepositoryBaseApiController<TModel>
        where TModel : class, IIdentity, new()
        where TRepository : class, IRepositoryWithIdentity<TModel> {
        protected readonly TRepository _repository;

        public RepositoryApiController(TRepository repository) {
            _repository = repository;
        }

        public override IEnumerable<TModel> Get() {
            return _repository.All();
        }

        protected override TModel GetEntity(string id) {
            return _repository.GetById(id);
        }

        protected override TModel InsertEntity(TModel value) {
            return _repository.Add(value);
        }

        protected override bool CanUpdateEntity(TModel original, Web.OData.Delta<TModel> value) {
            if (value.ContainsChangedProperty(t => t.Id) && !String.Equals(original.Id, value.GetEntity().Id, StringComparison.OrdinalIgnoreCase))
                return false;

            return base.CanUpdateEntity(original, value);
        }

        protected override TModel UpdateEntity(TModel original, Web.OData.Delta<TModel> value) {
            value.Patch(original);
            return _repository.Update(original);
        }

        protected override void DeleteEntity(TModel value) {
            _repository.Delete(value);
        }
    }
}