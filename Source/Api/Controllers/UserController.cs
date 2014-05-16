using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Models.User;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/user")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class UserController : RepositoryApiController<IUserRepository, User, ViewUser, User, User> {
        private readonly IOrganizationRepository _organizationRepository;

        public UserController(IUserRepository userRepository, IOrganizationRepository organizationRepository) : base(userRepository) {
            _organizationRepository = organizationRepository;
        }

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string organizationId = null, int page = 1, int limit = 10) {
            if (!CanAccessOrganization(organizationId))
                return NotFound();

            List<ViewUser> results = _repository.GetByOrganizationId(organizationId).Select(Mapper.Map<User, ViewUser>).ToList();
            var org = _organizationRepository.GetById(organizationId, true);
            if (org.Invites.Any())
                results.AddRange(org.Invites.Select(i => new ViewUser { EmailAddress = i.EmailAddress, IsInvite = true }));

            limit = GetLimit(limit);
            return OkWithResourceLinks(results.Skip(GetSkip(page, limit)).Take(limit).ToList(), results.Count > limit);
        }

        [HttpPost]
        [Route("{id}/admin-role")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult AddAdminRole(string id) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();

            if (!user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);
                _repository.Save(user, true);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{id}/admin-role")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult DeleteAdminRole(string id) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();

            if (user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                user.Roles.Remove(AuthorizationRoles.GlobalAdmin);
                _repository.Save(user, true);
            }

            return Ok();
        }

        protected override void CreateMaps() {
            Mapper.CreateMap<User, ViewUser>().AfterMap((u, vu) => {
                vu.HasAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin) && u.Roles.Contains(AuthorizationRoles.GlobalAdmin);
            });
            base.CreateMaps();
        }
    }
}