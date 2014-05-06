using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Exceptionless.Api.Models.User;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Models.Stats;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "user")]
    public class UserController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;

        public UserController(IUserRepository userRepository, IOrganizationRepository organizationRepository) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
        }

        [HttpPut]
        [Route("update-admin-role")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult UpdateAdminRole(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            var user = _userRepository.GetByIdCached(id);
            if (user == null)
                return NotFound();

            if (user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
                user.Roles.Remove(AuthorizationRoles.GlobalAdmin);
            else
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);

            _userRepository.Update(user);
            return Ok();
        }

        [HttpGet]
        [Route]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult GetByOrganizationId(string organizationId, int page = 1, int pageSize = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            List<ViewUser> results = _userRepository.GetByOrganizationId(organizationId).Select(u => 
                new ViewUser {
                    Id = u.Id, 
                    FullName = u.FullName, 
                    EmailAddress = u.EmailAddress, 
                    IsEmailAddressVerified = u.IsEmailAddressVerified, 
                    HasAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin) && u.Roles.Contains(AuthorizationRoles.GlobalAdmin)
                }).ToList();

            var organization = _organizationRepository.GetByIdCached(organizationId);
            if (organization.Invites.Any())
                results.AddRange(organization.Invites.Select(i => new ViewUser { EmailAddress = i.EmailAddress, IsInvite = true }));

            var result = new PagedResult<ViewUser>(results.Skip(skip).Take(pageSize).ToList(), results.Count) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            };

            return Ok(result);
        }
    }
}