using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models.User;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/users")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class UserController : RepositoryApiController<IUserRepository, User, ViewUser, User, UpdateUser> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IMailer _mailer;

        public UserController(IUserRepository userRepository, IOrganizationRepository organizationRepository, IMailer mailer) : base(userRepository) {
            _organizationRepository = organizationRepository;
            _mailer = mailer;
        }

        [HttpGet]
        [Route("me")]
        public IHttpActionResult GetCurrentUser() {
            return base.GetById(ExceptionlessUser.Id);
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetUserById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/users")]
        public IHttpActionResult GetByOrganizationId(string organizationId, int page = 1, int limit = 10) {
            if (!CanAccessOrganization(organizationId))
                return NotFound();

            List<ViewUser> results = _repository.GetByOrganizationId(organizationId).Select(Mapper.Map<User, ViewUser>).ToList();
            var org = _organizationRepository.GetById(organizationId, true);
            if (org.Invites.Any())
                results.AddRange(org.Invites.Select(i => new ViewUser { EmailAddress = i.EmailAddress, IsInvite = true }));

            page = GetPage(page);
            limit = GetLimit(limit);
            return OkWithResourceLinks(results.Skip(GetSkip(page, limit)).Take(limit).ToList(), results.Count > limit, page);
        }

        [HttpPatch]
        [HttpPut]
        [Route("{id:objectid}")]
        public override IHttpActionResult Patch(string id, Delta<UpdateUser> changes) {
            return base.Patch(id, changes);
        }

        [HttpPost]
        [Route("{id:objectid}/email-address/{email:minlength(1)}")]
        public IHttpActionResult UpdateEmailAddress(string id, string email) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();

            email = email.ToLower();
            if (!IsEmailAddressAvailableInternal(email))
                return BadRequest("A user with this email address already exists.");

            if (String.Equals(ExceptionlessUser.EmailAddress, email, StringComparison.OrdinalIgnoreCase))
                return Ok();

            user.EmailAddress = email;
            user.IsEmailAddressVerified = user.IsEmailAddressVerified || user.OAuthAccounts.Count(oa => String.Equals(oa.EmailAddress(), email, StringComparison.OrdinalIgnoreCase)) > 0;
            _repository.Save(user);

            if (!user.IsEmailAddressVerified)
                ResendVerificationEmail(id);

            return Ok(new { IsVerified = user.IsEmailAddressVerified });
        }

        [HttpGet]
        [Route("{id:objectid}/resend-verification-email")]
        public IHttpActionResult ResendVerificationEmail(string id) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();
            
            if (!user.IsEmailAddressVerified) {
                user.VerifyEmailAddressToken = Guid.NewGuid().ToString("N");
                user.VerifyEmailAddressTokenExpiration = DateTime.Now.AddMinutes(1440);
                _mailer.SendVerifyEmail(user);
            }

            return Ok();
        }

        [HttpPost]
        [Route("{id:objectid}/admin-role")]
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
        [Route("{id:objectid}/admin-role")]
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

            return StatusCode(HttpStatusCode.NoContent);
        }

        private bool IsEmailAddressAvailableInternal(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return false;

            if (ExceptionlessUser != null && String.Equals(ExceptionlessUser.EmailAddress, email, StringComparison.OrdinalIgnoreCase))
                return true;

            return _repository.GetByEmailAddress(email) == null;
        }

        protected override User GetModel(string id, bool useCache = true) {
            if (Request.IsGlobalAdmin() || String.Equals(ExceptionlessUser.Id, id))
                return base.GetModel(id, useCache);

            return null;
        }

        protected override ICollection<User> GetModels(string[] ids, bool useCache = true) {
            if (Request.IsGlobalAdmin())
                return base.GetModels(ids, useCache);

            return base.GetModels(ids.Where(id => String.Equals(ExceptionlessUser.Id, id)).ToArray(), useCache);
        }
    }
}