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
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Web.OData;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using Exceptionless.Web.Models.User;
using ServiceStack.CacheAccess;

namespace Exceptionless.Web.Controllers.Service {
    public class UserController : RepositoryApiController<User, IUserRepository> {
        private readonly ICacheClient _cacheClient;
        private readonly IOrganizationRepository _organizationRepository;

        public UserController(IUserRepository repository, IOrganizationRepository organizationRepository, ICacheClient cacheClient) : base(repository) {
            _organizationRepository = organizationRepository;
            _cacheClient = cacheClient;
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.Admin)]
        public override IEnumerable<User> Get() {
            return base.Get();
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.Admin)]
        public override User Get(string id) {
            return base.Get(id);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.Admin)]
        public override HttpResponseMessage Post(User value) {
            return base.Post(value);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.Admin)]
        public override HttpResponseMessage Put(string id, Delta<User> value) {
            return base.Put(id, value);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.Admin)]
        public override HttpResponseMessage Patch(string id, Delta<User> value) {
            return base.Patch(id, value);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.Admin)]
        public override HttpResponseMessage Delete(string id) {
            return base.Delete(id);
        }

        [HttpGet]
        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public PagedResult<UserModel> GetByOrganizationId(string organizationId, int page = 1, int pageSize = 10) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                throw new ArgumentException("Invalid organization id.", "organizationId"); // TODO: These should probably throw http Response exceptions.

            int skip = (page - 1) * pageSize;
            if (skip < 0)
                skip = 0;

            if (pageSize < 1)
                pageSize = 10;

            List<UserModel> results = _repository.GetByOrganizationId(organizationId).Select(u => new UserModel { Id = u.Id, FullName = u.FullName, EmailAddress = u.EmailAddress, IsEmailAddressVerified = u.IsEmailAddressVerified, HasAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin) && u.Roles.Contains(AuthorizationRoles.GlobalAdmin) }).ToList();

            var organization = _organizationRepository.GetByIdCached(organizationId);
            if (organization.Invites.Any())
                results.AddRange(organization.Invites.Select(i => new UserModel { EmailAddress = i.EmailAddress, IsInvite = true }));

            var result = new PagedResult<UserModel>(results.Skip(skip).Take(pageSize).ToList()) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10,
                TotalCount = results.Count
            };

            // TODO: Only return the populated fields (currently all properties are being returned).
            return result;
        }
    }
}