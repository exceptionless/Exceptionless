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
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Controllers;
using Exceptionless.Models.Stats;
using Exceptionless.Web.Models.User;

namespace Exceptionless.Web.Controllers.Service {
    public class UserController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;

        public UserController(IUserRepository userRepository, IOrganizationRepository organizationRepository) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
        }

        [HttpPut]
        [ExceptionlessAuthorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public HttpResponseMessage UpdateAdminRole(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequestErrorResponseMessage();

            var user = _userRepository.GetByIdCached(id);
            if(user == null)
                return NotFoundErrorResponseMessage(id);

            if (user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
                user.Roles.Remove(AuthorizationRoles.GlobalAdmin);
            else
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);

            _userRepository.Update(user);
            return new HttpResponseMessage(HttpStatusCode.OK);
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

            List<UserModel> results = _userRepository.GetByOrganizationId(organizationId).Select(u => new UserModel { Id = u.Id, FullName = u.FullName, EmailAddress = u.EmailAddress, IsEmailAddressVerified = u.IsEmailAddressVerified, HasAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin) && u.Roles.Contains(AuthorizationRoles.GlobalAdmin) }).ToList();

            var organization = _organizationRepository.GetByIdCached(organizationId);
            if (organization.Invites.Any())
                results.AddRange(organization.Invites.Select(i => new UserModel { EmailAddress = i.EmailAddress, IsInvite = true }));

            var result = new PagedResult<UserModel>(results.Skip(skip).Take(pageSize).ToList()) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10,
                TotalCount = results.Count
            };

            return result;
        }
    }
}